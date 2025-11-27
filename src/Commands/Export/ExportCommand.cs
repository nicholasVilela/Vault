using System.Collections.Concurrent;
using System.IO.Compression;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace Vault.Commands;

public class ExportCommand : AsyncCommand<ExportSettings> {
  const long OverheadUnitsPerGame = 1024 * 1024;

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
      .Select(f => f.Path)
      .Select(f => Path.Combine(f, "regions"))
      .Select(f => Path.Combine(f, settings.Region))
      .Select(f => Path.Combine(f, "versions"))
      .Select(f => Path.Combine(f, $"{settings.Version}.zip"))
      .Where(f => File.Exists(f))
      .Select(f => new FileInfo(f))
      .ToList();
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    var totalWork = FileHelper.TotalCopyBytes(files) + OverheadUnitsPerGame;
    if (settings.Extract) {
      totalWork += FileHelper.TotalExtractBytes(files);
    }

    var processedGames = 0;
    var errors = new ConcurrentBag<string>();
    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn(),
        new ElapsedTimeColumn())
      .UseRenderHook((renderable, tasks) => RenderHook(files.Count, settings, renderable, () => Volatile.Read(ref processedGames)))
      .StartAsync(async ctx => {
        var masterTask = ctx.AddTask(
          $"Master",
          autoStart: true,
          maxValue: totalWork
        );
        var semaphore = new SemaphoreSlim(100);
        var tasks = new List<Task>();

        foreach (var file in files) {
          var filePath = file.FullName;
          var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
          var name = parts[2].Split(" - ", 2)[1];
          var displayName = name.Replace("_", ":");

          tasks.Add(Task.Run(async () => {
            await semaphore.WaitAsync();
            await Export(file, name, settings, masterTask)
              .Catch(ex => {
                errors.Add($"[red]Error exporting {displayName}:[/] {ex.Message}");
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

  private async Task Export(FileInfo file, string name, ExportSettings settings, ProgressTask progress) {
    Directory.CreateDirectory(settings.WritePath);
    
    var destPath = $"{settings.WritePath}/{name}.zip";

    var fileSize = file.Length;

    var overheadRemaining = OverheadUnitsPerGame;
    var overheadStep = OverheadUnitsPerGame / 2;

    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    var copiedBytes = 0L;
    var copyProgress = new Progress<long>(bytes => {
      if (bytes <= 0) return;
      copiedBytes += bytes;
      progress.Increment(bytes);
    });
    await FileHelper.Copy(file.FullName, destPath, copyProgress);
    if (copiedBytes < fileSize) {
      progress.Increment(fileSize - copiedBytes);
    }


    if (settings.Extract) {
      var extractedBytes = 0L;
      var extractProgress = new Progress<long>(bytes => {
        if (bytes <= 0) return;
        extractedBytes += bytes;
        progress.Increment(bytes);
      });
      FileHelper.Extract(destPath, extractProgress);

      var totalExtractBytes = FileHelper.ExtractBytes(file);
      if (extractedBytes < totalExtractBytes) {
        progress.Increment(totalExtractBytes - extractedBytes);
      }
    }

    if (overheadRemaining > 0) {
      progress.Increment(overheadRemaining);
    }
  }

  private static IRenderable RenderHook(int fileCount, ExportSettings settings, IRenderable renderable, Func<int> getProcessedGames) {
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
