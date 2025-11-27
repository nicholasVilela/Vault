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

    var files = Directory.GetFiles(path, "*.zip*").Where(f => settings.Name == null ? true : Path.GetFileNameWithoutExtension(f) == settings.Name).ToList();
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
        var task = ctx.AddTask("Importing games", maxValue: files.Count);

        var semaphore = new SemaphoreSlim(8);
        var tasks = new List<Task>();

        foreach (var file in files) {
          await semaphore.WaitAsync(cancellationToken);

          var fileName = file.Replace("_", ":");

          var t = Task.Run(async () => {
            try {
              await ProcessGameAsync(file, $"{settings.Path}{settings.Console}/New", settings, igdb, cancellationToken);
            }
            catch(Exception ex) {
              AnsiConsole.MarkupLine(
                "[red]Error processing {0}:[/] {1}",
                Path.GetFileName(fileName),
                ex.Message
              );
            }
            finally {
              task.Increment(1);
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

  static string ExtractGuessTitle(string fileNameWithoutExt) {
    var parts = fileNameWithoutExt.Split('-', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length < 2) return fileNameWithoutExt.Trim();
    return parts[1].Trim();
  }

  static string SanitizeFileName(string name) {
    var invalid = Path.GetInvalidFileNameChars();
    var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
    return new string(chars);
  }

  static async Task ProcessGameAsync(
    string filePath,
    string baseDir,
    ImportSettings settings,
    IgdbService igdb,
    CancellationToken cancellationToken)
  {
    var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
    var guessedTitle = ExtractGuessTitle(fileNameNoExt.Replace("_", ":"));

    var platform = settings.Console;
    var region = settings.Region;
    var version = settings.Version;

    var igdbSearchName = guessedTitle;
    var game = await igdb.SearchGameAsync(igdbSearchName);

    if (game == null) {
      AnsiConsole.MarkupLine($"[yellow]No IGDB match for:[/] {igdbSearchName}");
      return;
    }

    var coverUrl = await igdb.SearchCoverUrlAsync(game.Id);
    var screenshots = await igdb.SearchScreenshotUrlsAsync(game.Id);

    var gameCode = Encoder.Encode(game.Id);  
    var gameFolderName = gameCode + " - " + SanitizeFileName(fileNameNoExt);
    var gameFolderPath = Path.Combine(baseDir, gameFolderName);
    var regionFolderPath = Path.Combine(gameFolderPath, "regions", region);
    var versionsFolderPath = Path.Combine(regionFolderPath, "versions");

    Directory.CreateDirectory(versionsFolderPath);

    var versionFilePath = Path.Combine(versionsFolderPath, version + ".zip");
    if (!File.Exists(versionFilePath)) File.Copy(filePath, versionFilePath, overwrite: false);

    var metadataPath = Path.Combine(gameFolderPath, "metadata.yaml");
    var yaml = BuildMetadataYaml(
      game.Name,
      game.Id,
      gameCode,
      platform,
      coverUrl,
      screenshots
    );

    await File.WriteAllTextAsync(metadataPath, yaml, cancellationToken);

    AnsiConsole.MarkupLine($"Processed [cyan]{game.Name}[/] → [blue]{gameFolderName}[/]");
  }

  static string BuildMetadataYaml(
    string title,
    int gameId,
    string gameCode,
    string platform,
    string coverUrl,
    List<string> screenshots)
  {
    var sb = new StringBuilder();

    var safeTitle = title.Replace("'", "''");

    sb.AppendLine("title: '" + safeTitle + "'");
    sb.AppendLine("game_id: " + gameId);
    sb.AppendLine("game_code: " + gameCode);
    sb.AppendLine("platform: " + platform);
    sb.AppendLine("media:");
    sb.AppendLine("  cover: " + (coverUrl ?? "''"));
    sb.AppendLine("  screenshots:");

    if (screenshots != null && screenshots.Count > 0) {
      foreach (var s in screenshots) sb.AppendLine("  - " + s);
    }

    return sb.ToString();
  }
}
