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
        new Markup("[grey]Version:[/]"),
        new Markup($"[yellow]{settings.Version}[/]")
      )
      .AddRow(
        new Markup("[grey]Destination:[/]"),
        new Markup($"[green]{settings.WritePath}[/]")
      );

    var header = new Panel(new Rows(renderable, grid)).RoundedBorder();

    return new Rows(header);
  }
}
