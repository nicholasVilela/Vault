using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Rendering;
using Vault.Commands;
using Vault.Message;
using Vault.Extensions;

namespace Vault.Helpers;

public static class ConsoleHelper {
  public static int Fail(string text) {
    AnsiConsole.MarkupLine($"[red]{text}[/]");
    return -1;
  }

  public static int Warning(string text) {
    AnsiConsole.MarkupLine($"[yellow]{text}[/]");
    return -1;
  }

  public static int Info(string text) {
    AnsiConsole.MarkupLine($"[cyan]{text}[/]");
    return -1;
  }

  public static IRenderable RenderHook(
    int fileCount,
    BaseSettings settings,
    IRenderable renderable,
    Func<int> getProcessedGames,
    bool displayPlatform,
    string suffix,
    ConcurrentBag<string> warnings
  ) {
    var titleGrid = new Grid()
      .AddColumn(new GridColumn().NoWrap())
      .AddRow(
        Align.Center(new Markup($"[bold][white]{settings.Title}[/][/]"))
      ).Expand();
    
    var gameLabel = string.IsNullOrEmpty(suffix) ? "" : fileCount == 1 ? suffix: $"{suffix}s";
    var infoGrid = new Grid()
      .AddColumn(new GridColumn().PadLeft(1))
      .AddColumn(new GridColumn().PadLeft(1))
      .AddRow(
        new Markup($"[grey]Processed:[/]"),
        new Markup($"[cyan]{getProcessedGames()}/{fileCount}[/] {(displayPlatform ? $"[green]{settings.Console}[/] " : "")}{gameLabel}")
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
        new Markup("[grey]Output:[/]"),
        new Markup($"[green]{settings.WritePath}[/]")
      );

    var title = new Panel(new Rows(titleGrid)).Header("[bold] COMMAND [/]").RoundedBorder();
    var info = new Panel(new Rows(infoGrid)).Header("[bold] INFO [/]").RoundedBorder();
    var progress = new Panel(new Rows(renderable)).Header("[bold] PROGRESS [/]").RoundedBorder();

    title.Width = 40;
    info.Width = 40;
    progress.Width = 40;

    var main = new Rows(
      title,
      info,
      progress
    );

    if (warnings.IsEmpty)
      return main;

    var warningRows = warnings
      .Select(warning => (IRenderable)new Markup($"[white]{warning}[/]"))
      .ToArray();

    var warningPanel = new Panel(new Rows(warningRows))
      .Header("[bold] WARNINGS [/]")
      .RoundedBorder()
      .BorderColor(Color.Yellow);

    // warningPanel.Width = 50;

    var layout = new Grid()
      .AddColumn(new GridColumn())
      .AddColumn(new GridColumn().Width(0))
      .AddColumn(new GridColumn())
      .AddRow(
        main,
        Text.Empty,
        warningPanel
      );

    return layout;
  }

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
    var errors = new ConcurrentBag<string>();
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
          messageSvc.Warnings))
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
              .Catch(ex => errors.Add($"[red]Error processing {displayName}:[/] {ex.Message}"))
              .Finally(() => {
                semaphore.Release();
                Interlocked.Increment(ref processedGames);
              });

            if (result != null) results.Add(result);
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
          messageSvc.Warnings))
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
