using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Rendering;
using Vault.Commands;
using Vault.Message;
using Vault.Extensions;

namespace Vault.Helpers;

public static partial class ConsoleHelper {
  public static async Task Build<TSettings, TResult>(
    List<FileInfo> files,
    TSettings settings,
    long totalWork,
    int maxConcurrency,
    Func<FileInfo, (string name, string displayName)> getNames,
    Func<FileInfo, string, string, ProgressTask, Task<TResult>> processFile,
    Func<List<TResult>, Task> finalize,
    bool displayPlatform,
    string suffix,
    MessageService messageSvc
  ) where TSettings : BaseSettings {
    var processedGames = 0;
    var results = new ConcurrentBag<TResult>();

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
            var result = await processFile(file, name, displayName, masterTask)
              .Catch(ex => messageSvc.Error($"[red]Error processing {displayName}:[/] {ex.Message}"))
              .Finally(() => {
                semaphore.Release();
                Interlocked.Increment(ref processedGames);
              });

            if (result != null) results.Add(result);
          }));
        }

        await Task.WhenAll(tasks);
      });

    if (finalize != null) {
      await finalize(results.ToList());
    }
  }

  public static async Task Build<TSettings>(
    List<FileInfo> files,
    TSettings settings,
    long totalWork,
    int maxConcurrency,
    Func<FileInfo, (string name, string displayName)> getNames,
    Func<FileInfo, string, string, ProgressTask, Task> processFile,
    bool displayPlatform,
    string suffix,
    MessageService messageSvc
  ) where TSettings : BaseSettings {
    var processedGames = 0;
    var errors = new ConcurrentBag<string>();

    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn())
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
              .Catch(ex => errors.Add($"[red]Error processing {displayName}:[/] {ex.Message}"))
              .Finally(() => {
                semaphore.Release();
                Interlocked.Increment(ref processedGames);
              });
          }));
        }

        await Task.WhenAll(tasks);
      });

    if (!errors.IsEmpty) {
      AnsiConsole.WriteLine();
      foreach (var err in errors) {
        AnsiConsole.MarkupLine(err);
      }
    }
  }
}
