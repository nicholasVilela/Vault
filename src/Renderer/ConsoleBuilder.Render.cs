using Spectre.Console;
using Spectre.Console.Rendering;
using Vault.Commands;
using Vault.Message;

namespace Vault.Renderer;

public static partial class ConsoleBuilder {
  public static IRenderable RenderHook(
    int fileCount,
    BaseSettings settings,
    IRenderable renderable,
    Func<int> getProcessedGames,
    Func<int> getSkippedGames,
    RenderOptions options,
    MessageService messageSvc
  ) {
    var width = 40;
    var title = RenderTitle(settings, width);
    var info = RenderInfo(settings, fileCount, options, getProcessedGames, getSkippedGames, width);
    var progress = RenderProgress(renderable, width);
    var warnings = RenderWarnings(messageSvc);
    var errors = RenderErrors(messageSvc);

    var main = new Rows(
      title,
      info,
      progress
    );

    var layout = new Grid()
      .AddColumn(new GridColumn())
      .AddColumn(new GridColumn().Width(0))
      .AddColumn(new GridColumn())
      .AddRow(
        main,
        Text.Empty,
        new Rows(errors, warnings)
      );

    return layout;
  }

  private static Panel RenderTitle(BaseSettings settings, int width) {
    var grid =  new Grid()
      .AddColumn(new GridColumn().NoWrap())
      .AddRow(
        Align.Center(new Markup($"[bold][white]{settings.Title}[/][/]"))
      ).Expand();

    var panel = new Panel(new Rows(grid))
    .Header("[bold] COMMAND [/]")
    .RoundedBorder();

    panel.Width = width;

    return panel;
  }

  private static Panel RenderInfo(BaseSettings settings, int fileCount, RenderOptions options, Func<int> getProcessedGames, Func<int> getSkippedGames, int width) {
    var gameLabel = string.IsNullOrEmpty(options.Suffix) ? "" : fileCount == 1 ? options.Suffix: $"{options.Suffix}s";
    var grid =  new Grid()
      .AddColumn(new GridColumn().PadLeft(0))
      .AddColumn(new GridColumn().PadLeft(1))
      .AddRow(
        new Markup($"[grey]Processed:[/]"),
        new Markup($"[cyan]{getProcessedGames()}/{fileCount}[/] {(options.DisplayPlatform ? $"[green]{settings.Console}[/] " : "")}{gameLabel}")
      )
      .AddRow(
        new Markup($"[grey]Skipped:[/]"),
        new Markup($"[cyan]{getSkippedGames()}[/]")
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

    var panel = new Panel(new Rows(grid))
      .Header("[bold] INFO [/]")
      .RoundedBorder();

    panel.Width = width;

    return panel;
  }

  private static Panel RenderProgress(IRenderable renderable, int width) {
    var panel = new Panel(new Rows(renderable))
      .Header("[bold] PROGRESS [/]")
      .RoundedBorder();

    panel.Width = width;

    return panel;
  }

  private static Renderable RenderWarnings(MessageService messageSvc) {
    var warnings = messageSvc.Warnings;
    if (warnings.IsEmpty) return new Markup("");

    var rows = warnings
      .Select(warning => (IRenderable)new Markup($"[white]{warning}[/]"))
      .ToArray();

    var panel = new Panel(new Rows(rows))
      .Header("[bold] WARNINGS [/]")
      .RoundedBorder()
      .BorderColor(Color.Yellow);

    return panel;
  }

  private static Renderable RenderErrors(MessageService messageSvc) {
    var errors = messageSvc.Errors;
    if (errors.IsEmpty) return new Markup("");

    var rows = errors
      .Select(error => (IRenderable)new Markup($"[white]{error}[/]"))
      .ToArray();

    var panel = new Panel(new Rows(rows))
      .Header("[bold] ERRORS [/]")
      .RoundedBorder()
      .BorderColor(Color.Red);

    return panel;
  }
}
