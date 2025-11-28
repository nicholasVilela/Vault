using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Rendering;
using Vault.Commands;

namespace Vault;

public static class ConsoleHelper {
  public static int Fail(string text) {
    AnsiConsole.MarkupLine($"[red]{text}[/]");
    return -1;
  }

  public static int Warning(string text) {
    AnsiConsole.MarkupLine($"[yellow]{text}[/]");
    return -1;
  }

  public static IRenderable RenderHook(int fileCount, BaseSettings settings, IRenderable renderable, Func<int> getProcessedGames) {
    var gameLabel = fileCount == 1 ? "game" : "games";

    var grid = new Grid()
      .AddColumn(new GridColumn().PadLeft(1))
      .AddColumn(new GridColumn().PadLeft(1))
      .AddRow(
        new Markup($"[bold]{settings.Title}:[/]"),
        new Markup($"[cyan]{getProcessedGames()}/{fileCount}[/] [green]{settings.Console}[/] {gameLabel}")
      )
      .AddRow(
        new Markup("[grey]Name:[/]"),
        new Markup($"[yellow]{settings.Name ?? "*"}[/]")
      )
      .AddRow(
        new Markup("[grey]Region:[/]"),
        new Markup($"[yellow]{settings.Region}[/]")
      )
      .AddRow(
        new Markup("[grey]Version:[/]"),
        new Markup($"[yellow]{settings.Version}[/]")
      )
      .AddRow(
        new Markup("[grey]Destination:[/]"),
        new Markup($"[green]{settings.WritePath}[/]")
      );

    var bar = new Panel(new Rows(renderable)).RoundedBorder();
    var header = new Panel(new Rows(grid)).RoundedBorder();

    bar.Width = 50;
    header.Width = 50;

    return new Rows(header, bar);
  }

  public static async Task Build<TSettings>(
    List<FileInfo> files,
    TSettings settings,
    long totalWork,
    int maxConcurrency,
    Func<FileInfo, (string name, string displayName)> getNames,
    Func<FileInfo, string, string, TSettings, ProgressTask, Task> processFile
  ) where TSettings : BaseSettings {
    var processedGames = 0;
    var errors = new ConcurrentBag<string>();

    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn(),
        new ElapsedTimeColumn())
      .UseRenderHook((renderable, tasks) =>
        RenderHook(
          files.Count,
          settings,
          renderable,
          () => Volatile.Read(ref processedGames)))
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
            await processFile(file, name, displayName, settings, masterTask)
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
