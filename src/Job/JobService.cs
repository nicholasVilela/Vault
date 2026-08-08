using Spectre.Console;
using Vault.Commands;
using Vault.Extensions;
using Vault.Message;
using Vault.Renderer;

namespace Vault.Job;

public class JobService<TSettings> : IDisposable where TSettings : BaseSettings {
  public void Dispose() {}

  private readonly MessageService _messageSvc;

  private int _processed { get; set; }
  private int _skipped { get; set; }
  private int _fileCount { get => Volatile.Read(ref field); set => Volatile.Write(ref field, value); }

  public TSettings Settings { get; set; }
  public JobOptions JobOptions { get; set; }
  public RenderOptions RenderOptions { get; set; }

  public Func<FileInfo, (string name, string displayName)> OnGetNames { get; set; }
  public Func<FileInfo, string, string, ProgressTask, Task> OnProcess { get; set; }
  public Func<TSettings, List<FileInfo>> OnGetFiles { get; set; }
  public Func<List<FileInfo>, long> OnGetWork { get; set; }
  public Func<bool> OnValidate { get; set; }
  public Action OnFinalize { get; set; }


  public JobService(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }

  public async Task Run() {
    var processed = _processed;
    var skipped = _skipped;

    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn()
        )
      .UseRenderHook((renderable, tasks) =>
        ConsoleRenderer.RenderHook(
          _fileCount,
          Settings,
          renderable,
          () => Volatile.Read(ref processed),
          () => Volatile.Read(ref skipped),
          RenderOptions,
          _messageSvc))
      .StartAsync(async ctx => {
        if (OnValidate != null && !OnValidate()) return;

        var files = OnGetFiles(Settings);
        if (files.Count == 0) _messageSvc.Error($"No game files found in: '{Settings.ReadPath}'{(!string.IsNullOrEmpty(Settings.Name) ? $" with name: '{Settings.Name}'" : "")}");

        _fileCount = files.Count;

        var masterTask = ctx.AddTask(
          "Master",
          autoStart: true,
          maxValue: OnGetWork(files)
        );

        var semaphore = new SemaphoreSlim(JobOptions.MaxThreads);
        var tasks = new List<Task>();

        foreach (var file in files) {
          var (name, displayName) = OnGetNames(file);

          tasks.Add(Task.Run(async () => {
            await semaphore.WaitAsync();
            await OnProcess(file, name, displayName, masterTask)
              .Catch(ex => _messageSvc.Error($"Error processing {displayName}: {ex.Message}"))
              .Finally(() => {
                semaphore.Release();
                Interlocked.Increment(ref processed);
              });
          }));
        }

        await Task.WhenAll(tasks);
      });

    if (OnFinalize != null) OnFinalize();
  }
}
