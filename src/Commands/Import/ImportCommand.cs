using System.Collections.Concurrent;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.IGDB;

namespace Vault.Commands;

public class ImportCommand : AsyncCommand<ImportSettings> {
  const long OverheadUnitsPerGame = 1024 * 1024;

  public override async Task<int> ExecuteAsync(CommandContext context, ImportSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return ConsoleHelper.Fail("--console is required");
    if (!Directory.Exists(settings.ReadPath)) return ConsoleHelper.Fail($"Path does not exist: {settings.ReadPath}");

    var clientId = Environment.GetEnvironmentVariable("IGDB_CLIENT_ID");
    if (string.IsNullOrWhiteSpace(clientId)) return ConsoleHelper.Fail("Missing IGDB_CLIENT_ID environment variable.");

    var clientSecret = Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET");
    if (string.IsNullOrWhiteSpace(clientSecret)) return ConsoleHelper.Fail("Missing IGDB_CLIENT_SECRET environment variable.");

    var files = Directory
      .GetFiles(settings.ReadPath, "*.zip*")
      .Where(f => settings.Name == null ? true : Path.GetFileNameWithoutExtension(f) == settings.Name)
      .Select(f => new FileInfo(f))
      .ToList();
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    using var igdb = new IgdbService(clientId, clientSecret);

    var processedGames = 0;
    var errors = new ConcurrentBag<string>();
    var totalWork = FileHelper.TotalCopyBytes(files) + OverheadUnitsPerGame;
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

        var semaphore = new SemaphoreSlim(8);
        var tasks = new List<Task>();

        foreach (var file in files) {
          var filePath = file.FullName;
          var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
          var displayName = fileNameNoExt.Replace("_", ":");

          tasks.Add(Task.Run(async () => {
            await semaphore.WaitAsync();
            await Import(
              file,
              settings,
              igdb,
              masterTask)
              .Catch(ex => {
                errors.Add($"[red]Error processing {displayName}:[/] {ex.Message}");
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

  static async Task Import(
    FileInfo fileInfo,
    ImportSettings settings,
    IgdbService igdb,
    ProgressTask progress
    ) {
    var filePath = fileInfo.FullName;
    var fileSize = fileInfo.Length;

    var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
    var displayName = fileNameNoExt.Replace("_", ":");

    var overheadRemaining = OverheadUnitsPerGame;

    var game = await igdb.SearchGameAsync(displayName);

    if (game == null) {
      AnsiConsole.MarkupLine($"[yellow]No IGDB match for:[/] {displayName}");
      progress.Increment(fileSize + overheadRemaining);
      return;
    }

    var overheadStep = OverheadUnitsPerGame / 4;

    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    var coverTaskCall = igdb.SearchCoverUrlAsync(game.Id);
    var shotsTaskCall = igdb.SearchScreenshotUrlsAsync(game.Id);

    await coverTaskCall;
    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    await shotsTaskCall;
    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    var coverUrl = coverTaskCall.Result;
    var screenshots = shotsTaskCall.Result;

    var gameCode = Encoder.Encode(game.Id);
    var gameFolderName = gameCode + " - " + fileNameNoExt;
    var gameFolderPath = Path.Combine(settings.WritePath, gameFolderName);
    var regionFolderPath = Path.Combine(gameFolderPath, "regions", settings.Region);
    var versionsFolderPath = Path.Combine(regionFolderPath, "versions");

    Directory.CreateDirectory(versionsFolderPath);

    var versionFilePath = Path.Combine(versionsFolderPath, settings.Version + ".zip");
    var copiedForThisFile = 0L;
    var copyProgress = new Progress<long>(bytes => {
      if (bytes <= 0) return;
      copiedForThisFile += bytes;
      progress.Increment(bytes);
    });
    await FileHelper.Copy(filePath, versionFilePath, copyProgress);

    if (copiedForThisFile < fileSize) {
      progress.Increment(fileSize - copiedForThisFile);
    }

    Metadata.BuildAndWrite(
      game.Name,
      game.Id,
      gameCode,
      settings.Console,
      coverUrl,
      screenshots,
      gameFolderPath
    );

    if (overheadRemaining > 0) {
      progress.Increment(overheadRemaining);
    }
  }

  private static IRenderable RenderHook(int fileCount, ImportSettings settings, IRenderable renderable, Func<int> getProcessedGames) {
    var gameLabel = fileCount == 1 ? "game" : "games";

    var grid = new Grid();
    grid.AddColumn(new GridColumn());
    grid.AddColumn(new GridColumn());

    grid.AddRow(
      new Markup("[bold]Imported:[/]"),
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
    header.Padding(new Padding(0, 0, 0,0));

    return new Rows(header);
  }
}
