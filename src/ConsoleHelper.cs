using Spectre.Console;

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
}
