using System.Collections.Concurrent;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Vault.IGDB;

namespace Vault.Commands;

public class ImportCommand : AsyncCommand<ImportSettings> {
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
      .ToList();
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    AnsiConsole.MarkupLine($"Importing {files.Count} [green]{settings.Console}[/] game{(files.Count > 1 ? "s" : "")} (region: [yellow]{settings.Region}[/], version: [yellow]{settings.Version}[/], name: [cyan]{settings.Name ?? "any"}[/])");

    using var igdb = new IgdbService(clientId, clientSecret);
    var errors = new ConcurrentBag<string>();
    await AnsiConsole.Progress()
      .Columns(
        new TaskDescriptionColumn(),
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn())
      .StartAsync(async ctx => {
        var semaphore = new SemaphoreSlim(8);
        var tasks = new List<Task>();

        foreach (var file in files) {
          var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
          var displayName = fileNameNoExt.Replace("_", ":");

          var fileLength = new FileInfo(file).Length;
          var maxValue = fileLength + 4;
          var gameTask = ctx.AddTask(displayName, autoStart: true, maxValue: maxValue);

          tasks.Add(Task.Run(async () => {
            await semaphore.WaitAsync();
            await ProcessGameAsync(
              file,
              fileLength,
              settings,
              igdb,
              gameTask)
              .Catch(ex => errors.Add($"[red]Error processing {displayName}:[/] {ex.Message}"))
              .Finally(() => semaphore.Release());
          }));
        }

        await Task.WhenAll(tasks);
    });

    if (!errors.IsEmpty) {
      AnsiConsole.WriteLine();
      foreach (var err in errors) AnsiConsole.MarkupLine(err);
    }

    AnsiConsole.MarkupLine("[green]Import completed.[/]");
    return 0;
  }

  static async Task ProcessGameAsync(
    string filePath,
    long fileLength,
    ImportSettings settings,
    IgdbService igdb,
    ProgressTask progress
    ) {
    var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
    var displayName = fileNameNoExt.Replace("_", ":");

    var game = await igdb.SearchGameAsync(displayName);
    progress.Increment(1);

    if (game == null) {
      AnsiConsole.MarkupLine($"[yellow]No IGDB match for:[/] {displayName}");
      progress.Increment(progress.MaxValue);
      return;
    }

    var coverTaskCall = igdb.SearchCoverUrlAsync(game.Id);
    var shotsTaskCall = igdb.SearchScreenshotUrlsAsync(game.Id);

    await coverTaskCall;
    progress.Increment(1);

    await shotsTaskCall;
    progress.Increment(1);

    var coverUrl = coverTaskCall.Result;
    var screenshots = shotsTaskCall.Result;

    var gameCode = Encoder.Encode(game.Id);
    var gameFolderName = gameCode + " - " + fileNameNoExt;
    var gameFolderPath = Path.Combine(settings.WritePath, gameFolderName);
    var regionFolderPath = Path.Combine(gameFolderPath, "regions", settings.Region);
    var versionsFolderPath = Path.Combine(regionFolderPath, "versions");

    Directory.CreateDirectory(versionsFolderPath);

    var versionFilePath = Path.Combine(versionsFolderPath, settings.Version + ".zip");
    if (File.Exists(versionFilePath)) {
      progress.Increment(fileLength);
    } else {
      await FileHelper.Copy(
        filePath,
        versionFilePath
      );
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

    progress.Increment(1);
  }
}
