using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Rendering;
using Vault.Commands;
using Vault.Message;
using Vault.Extensions;

namespace Vault.Helpers;

public static partial class ConsoleHelper {
  public static async Task Build<TSettings>(
    List<FileInfo> files,
    TSettings settings,
    long totalWork,
    int maxConcurrency,
    Func<FileInfo, (string name, string displayName)> getNames,
    Func<FileInfo, string, string, ProgressTask, Task> processFile,
    bool displayPlatform,
    string suffix,
    MessageService messageSvc,
    Action finalize = null
  ) where TSettings : BaseSettings {
    var processedGames = 0;

    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn()
        )
      .UseRenderHook((renderable, tasks) =>
        RenderHook(
          files.Count,
          settings,
          renderable,
          () => Volatile.Read(ref processedGames),
          displayPlatform,
          suffix,
          messageSvc))
      .StartAsync(async ctx => {
        var masterTask = ctx.AddTask(
          "Master",
          autoStart: true,
          maxValue: totalWork
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
