using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace Vault.Commands;

public class ExportCommand : AsyncCommand<ExportSettings> {
  public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return ConsoleHelper.Fail("--console is required");
    if (!Directory.Exists(settings.ReadPath)) return ConsoleHelper.Fail($"Path does not exist: {settings.ReadPath}");

    var files = Directory.EnumerateDirectories(settings.ReadPath)
      .Where(f => Path.GetFileName(f).Contains(" - "))
      .Select(f => new {
        Path = f,
        Name = f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[2].Split(" - ", 2)[1]
      })
      .Where(f => settings.Name == null ? true : Path.GetFileNameWithoutExtension(f.Name) == settings.Name)
      .OrderBy(f => f.Name)
      .Select(f => f.Path)
      .Select(f => Path.Combine(f, "regions"))
      .Select(f => Path.Combine(f, settings.Region))
      .Select(f => Path.Combine(f, "versions"))
      .Select(f => Path.Combine(f, $"{settings.Version}.zip"))
      .Where(f => File.Exists(f))
      .ToList();
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    var processedGames = 0;
    var errors = new ConcurrentBag<string>();
    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn(),
        new ElapsedTimeColumn())
      .UseRenderHook((renderable, tasks) => RenderHook(files.Count, settings, tasks, renderable, () => Volatile.Read(ref processedGames)))
      .StartAsync(async ctx => {
        var masterTask = ctx.AddTask(
          $"Master",
          autoStart: true,
          maxValue: files.Count
        );
        var semaphore = new SemaphoreSlim(100);
        var tasks = new List<Task>();

        foreach (var file in files) {
          var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
          var name = parts[2].Split(" - ", 2)[1];
          var displayName = name.Replace("_", ":");

          tasks.Add(Task.Run(async () => {
            await semaphore.WaitAsync();
            await CopyFile(file, name, settings, masterTask)
              .Catch(ex => {
                errors.Add($"[red]Error exporting {displayName}:[/] {ex.Message}");
                masterTask.Increment(1);
              })
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
      foreach (var err in errors) AnsiConsole.MarkupLine(err);
    }

    return 0;
  }

  private async Task CopyFile(string path, string name, ExportSettings settings, ProgressTask progress) {
    Directory.CreateDirectory(settings.WritePath);
    
    var destPath = $"{settings.WritePath}/{name}.zip";
    await FileHelper.Copy(path, destPath);
    if (settings.Extract) FileHelper.Extract(destPath);

    progress.Increment(1);
  }

  private static IRenderable RenderHook(int fileCount, ExportSettings settings, IReadOnlyList<ProgressTask> tasks, IRenderable renderable, Func<int> getProcessedGames) {
    var gameLabel = fileCount == 1 ? "game" : "games";

    var grid = new Grid();
    grid.AddColumn(new GridColumn());
    grid.AddColumn(new GridColumn());

    grid.AddRow(
      new Markup("[bold]Exported:[/]"),
      new Markup($"[cyan]{getProcessedGames()}/{fileCount}[/] [green]{settings.Console}[/] {gameLabel}")
    );

    grid.AddRow(
      new Markup("[grey]Name:[/]"),
      new Markup($"[yellow]{settings.Name ?? "any"}[/]")
    );

    grid.AddRow(
      new Markup("[grey]Region:[/]"),
      new Markup($"[yellow]{settings.Region}[/]")
    );

    grid.AddRow(
      new Markup("[grey]Version:[/]"),
      new Markup($"[yellow]{settings.Version}[/]")
    );

    grid.AddRow(
      new Markup("[grey]Destination:[/]"),
      new Markup($"[green]{settings.WritePath}[/]")
    );

    var header = new Panel(new Rows(renderable, grid)).RoundedBorder();

    return new Rows(header);
  }
}
