using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Vault.IGDB;

namespace Vault.Commands;

public class ImportCommand : AsyncCommand<ImportSettings> {
  public override async Task<int> ExecuteAsync(CommandContext context, ImportSettings settings, CancellationToken cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) {
      AnsiConsole.MarkupLine("[red]--console is required[/]");
      return -1;
    }

    var path = $"{settings.Path}{settings.Console}/Games";
    if (!Directory.Exists(path)) {
      AnsiConsole.MarkupLine("[red]Path does not exist:[/] " + path);
      return -1;
    }

    var clientId = Environment.GetEnvironmentVariable("IGDB_CLIENT_ID");
    var clientSecret = Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET");
    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)) {
      AnsiConsole.MarkupLine("[red]Missing IGDB_CLIENT_ID or IGDB_CLIENT_SECRET environment variables.[/]");
      return -1;
    }

    var files = Directory
      .GetFiles(path, "*.zip*")
      .Where(f => settings.Name == null ? true : Path.GetFileNameWithoutExtension(f) == settings.Name)
      .ToList();

    if (files.Count == 0) {
      AnsiConsole.MarkupLine("[yellow]No game files found in:[/] " + path);
      return 0;
    }

    AnsiConsole.MarkupLine($"Importing {files.Count} [green]{settings.Console}[/] game{(files.Count > 1 ? "s" : "")} (region: [yellow]{settings.Region}[/], version: [yellow]{settings.Version}[/], name: [cyan]{settings.Name ?? "any"}[/])");

    using var igdb = new IgdbService(clientId, clientSecret);

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

          var t = Task.Run(async () => {
            await semaphore.WaitAsync(cancellationToken);
            try {
              await ProcessGameAsync(
                file,
                fileLength,
                $"{settings.Path}{settings.Console}",
                settings,
                igdb,
                gameTask,
                cancellationToken
              );
            }
            catch (Exception ex) {
              AnsiConsole.MarkupLine(
                "[red]Error processing {0}:[/] {1}",
                displayName,
                ex.Message
              );
            }
            finally {
              semaphore.Release();
            }
          }, cancellationToken);

          tasks.Add(t);
        }

        await Task.WhenAll(tasks);
      });

    AnsiConsole.MarkupLine("[green]Import completed.[/]");
    return 0;
  }

  static async Task ProcessGameAsync(
    string filePath,
    long fileLength,
    string baseDir,
    ImportSettings settings,
    IgdbService igdb,
    ProgressTask progress,
    CancellationToken cancellationToken
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
    var gameFolderPath = Path.Combine(baseDir, gameFolderName);
    var regionFolderPath = Path.Combine(gameFolderPath, "regions", settings.Region);
    var versionsFolderPath = Path.Combine(regionFolderPath, "versions");

    Directory.CreateDirectory(versionsFolderPath);

    var versionFilePath = Path.Combine(versionsFolderPath, settings.Version + ".zip");
    if (File.Exists(versionFilePath)) {
      progress.Increment(fileLength);
    } else {
      await CopyFileWithProgressAsync(
        filePath,
        versionFilePath,
        fileLength,
        progress,
        cancellationToken
      );
    }

    Metadata.BuildAndWrite(
      game.Name,
      game.Id,
      gameCode,
      settings.Console,
      coverUrl,
      screenshots,
      gameFolderPath,
      cancellationToken
    );

    progress.Increment(1);
  }

  static async Task CopyFileWithProgressAsync(
  string sourcePath,
  string destPath,
  long fileLength,
  ProgressTask progress,
  CancellationToken cancellationToken
  ) {
    const int bufferSize = 81920;
    var buffer = new byte[bufferSize];

    await using var source = new FileStream(
      sourcePath,
      FileMode.Open,
      FileAccess.Read,
      FileShare.Read
    );

    await using var dest = new FileStream(
      destPath,
      FileMode.Create,
      FileAccess.Write,
      FileShare.None
    );

    long copied = 0;

    while (true) {
      var read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
      if (read == 0)
        break;

      await dest.WriteAsync(buffer, 0, read, cancellationToken);
      copied += read;

      progress.Increment(read);
    }
  }
}
