using Spectre.Console;
using Vault.Commands;
using Vault.Extensions;
using Vault.Message;
using Vault.Renderer;

namespace Vault.Job;

public class JobDispatcher<TSettings> where TSettings : BaseSettings {
  private Func<FileInfo, (string name, string displayName)> _onGetNames { get; set; }
  private Func<FileInfo, string, string, ProgressTask, Task<JobResult>> _onProcess { get; set; }
  private Func<TSettings, List<FileInfo>> _onGetFiles { get; set; }
  private Func<List<FileInfo>, long> _onGetWork { get; set; }
  private Action _onFinalize { get; set; }

  private Dictionary<Func<bool>, Action> _assertions { get; set; } = new();

  private JobOptions _jobOptions { get; set; }
  private RenderOptions _renderOptions { get; set; }
  private TSettings _settings { get; set; }

  private int _fileCount { get => Volatile.Read(ref field); set => Volatile.Write(ref field, value); }

  public JobDispatcher<TSettings> GetNames(Func<FileInfo, (string name, string displayName)> func) {
    _onGetNames += func;
    return this;
  }

  public JobDispatcher<TSettings> GetProcess(Func<FileInfo, string, string, ProgressTask, Task<JobResult>> func) {
    _onProcess += func;
    return this;
  }
  
  public JobDispatcher<TSettings> GetFiles(Func<TSettings, List<FileInfo>> func) {
    _onGetFiles += func;
    return this;
  }

  public JobDispatcher<TSettings> GetWork(Func<List<FileInfo>, long> func) {
    _onGetWork += func;
    return this;
  }

  public JobDispatcher<TSettings> Finalize(Action func) {
    _onFinalize += func;
    return this;
  }

  public JobDispatcher<TSettings> Assert(Func<bool> func, Action action) {
    _assertions.Add(func, action);
    return this;
  }

  public JobDispatcher<TSettings> WithJobOptions(JobOptions options) {
    _jobOptions = options;
    return this;
  }

  public JobDispatcher<TSettings> WithRenderOptions(RenderOptions options) {
    _renderOptions = options;
    return this;
  }
  
  public JobDispatcher<TSettings> WithSettings(TSettings settings) {
    _settings = settings;
    return this;
  }

  public async Task Run(MessageService messageSvc) {
    var processed = 0;
    var skipped = 0;

    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn()
        )
      .UseRenderHook((renderable, tasks) =>
        ConsoleRenderer.RenderHook(
          _fileCount,
          _settings,
          renderable,
          () => Volatile.Read(ref processed),
          () => Volatile.Read(ref skipped),
          _renderOptions,
          messageSvc))
      .StartAsync(async ctx => {
        foreach (var (assertion, action) in _assertions) {
          if (!assertion()) continue;

          action();
          return;
        }

        var files = _onGetFiles(_settings);
        if (files.Count == 0) messageSvc.Error($"No game files found in: '{_settings.ReadPath}'{(!string.IsNullOrEmpty(_settings.Name) ? $" with name: '{_settings.Name}'" : "")}");

        _fileCount = files.Count;

        var masterTask = ctx.AddTask(
          "Master",
          autoStart: true,
          maxValue: _onGetWork(files)
        );

        var semaphore = new SemaphoreSlim(_jobOptions.MaxThreads);
        var tasks = new List<Task>();

        foreach (var file in files) {
          var (name, displayName) = _onGetNames(file);

          tasks.Add(Task.Run(async () => {
            await semaphore.WaitAsync();
            await _onProcess(file, name, displayName, masterTask)
              .OnSkipAsync(async () => Interlocked.Increment(ref skipped))
              .Catch(ex => messageSvc.Error($"Error processing {displayName}: {ex.Message}"))
              .Finally(() => {
                semaphore.Release();
                Interlocked.Increment(ref processed);
              });
          }));
        }

        await Task.WhenAll(tasks);
      });

    if (_onFinalize != null) _onFinalize();
  }
}
