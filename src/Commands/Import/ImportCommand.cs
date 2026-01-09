using System.Collections.Concurrent;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.Helper;
using Vault.Helpers;
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

    var files = GetFiles(settings);
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    using var igdb = new IgdbService(clientId, clientSecret);

    await ConsoleHelper.Build(
      files,
      settings,
      totalWork: FileHelper.TotalCopyBytes(files) + OverheadUnitsPerGame * files.Count,
      maxConcurrency: 100,
      processFile: (file, name, displayName, task) => Import(file, name, displayName, settings, task, igdb),
      getNames: file => {
        var filePath = file.FullName;
        var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
        var displayName = fileNameNoExt.Replace("_", ":");
        return (fileNameNoExt, displayName);
      }
    );

    return 0;
  }

  static async Task Import(
    FileInfo fileInfo,
    string name,
    string displayName,
    ImportSettings settings,
    ProgressTask progress,
    IgdbService igdb
    ) {
    var filePath = fileInfo.FullName;
    var fileSize = fileInfo.Length;

    var overheadRemaining = OverheadUnitsPerGame;
    var overheadStep = OverheadUnitsPerGame / 3;

    var game = await igdb.SearchGameAsync(displayName, settings.Console);
    if (game == null) {
      AnsiConsole.MarkupLine($"[yellow]No IGDB match for:[/] {displayName}");
      progress.Increment(fileSize + overheadRemaining);
      return;
    }

    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    var (cover, screenshots) = await igdb.GetMediaAsync(game.Id);
    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    var gameCode = Encoder.Encode(game.Id);
    var gameFolderName = gameCode + " - " + name;
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

    if (settings.Move) FileHelper.Move(filePath, versionFilePath, copyProgress);
    else await FileHelper.Copy(filePath, versionFilePath, copyProgress);

    if (copiedForThisFile < fileSize) {
      progress.Increment(fileSize - copiedForThisFile);
    }

    MetadataHelper.BuildAndWrite(
      game.Name,
      game.Id,
      gameCode,
      settings.Console,
      game.Summary,
      cover,
      screenshots,
      gameFolderPath
    );

    if (overheadRemaining > 0) {
      progress.Increment(overheadRemaining);
    }
  }

  public List<FileInfo> GetFiles(ImportSettings settings ) {
    return Directory
      .GetFiles(settings.ReadPath, "*.zip*")
      .Where(f => string.IsNullOrEmpty(settings.Name) || Path.GetFileNameWithoutExtension(f) == settings.Name)
      .Select(f => new FileInfo(f))
      .ToList();
  }
}
