using Spectre.Console;
using Vault.Commands;
using Vault.Message;
using Vault.Extensions;

namespace Vault.Renderer;

public static partial class ConsoleBuilder {
  public static async Task Build<TSettings>(
    Func<TSettings, List<FileInfo>> getFiles,
    TSettings settings,
    Func<List<FileInfo>, long> totalWork,
    int maxConcurrency,
    Func<FileInfo, (string name, string displayName)> getNames,
    Func<FileInfo, string, string, ProgressTask, Task> processFile,
    bool displayPlatform,
    string suffix,
    MessageService messageSvc,
    Func<bool> validate = null,
    Action finalize = null
  ) where TSettings : BaseSettings {
    var processedGames = 0;
    var skippedGames = 0;
    var fileCount = 0;

    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn()
        )
      .UseRenderHook((renderable, tasks) =>
        RenderHook(
          Volatile.Read(ref fileCount),
          settings,
          renderable,
          () => Volatile.Read(ref processedGames),
          () => Volatile.Read(ref skippedGames),
          displayPlatform,
          suffix,
          messageSvc))
      .StartAsync(async ctx => {
        if (validate != null && !validate()) return;

        var files = getFiles(settings);
        if (files.Count == 0) messageSvc.Error($"No game files found in: '{settings.ReadPath}'{(!string.IsNullOrEmpty(settings.Name) ? $" with name: '{settings.Name}'" : "")}");

        Volatile.Write(ref fileCount, files.Count);

        var masterTask = ctx.AddTask(
          "Master",
          autoStart: true,
          maxValue: totalWork(files)
        );

        var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task>();

        foreach (var file in files) {
          var (name, displayName) = getNames(file);

          tasks.Add(Task.Run(async () => {
            await semaphore.WaitAsync();
            await processFile(file, name, displayName, masterTask)
              .Catch(ex => messageSvc.Error($"Error processing {displayName}: {ex.Message}"))
              .Finally(() => {
                semaphore.Release();
                Interlocked.Increment(ref processedGames);
              });
          }));
        }

        await Task.WhenAll(tasks);
      });

    if (finalize != null) finalize();
  }
}
