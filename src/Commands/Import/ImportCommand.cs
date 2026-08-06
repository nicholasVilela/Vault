using System.Collections.Concurrent;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.Helpers;
using Vault.IGDB;
using Vault.IGDB.Data;

namespace Vault.Commands;

public class ImportCommand : AsyncCommand<ImportSettings> {
  private readonly IgdbService _igdbSvc;
  public ImportCommand(IgdbService igdbSvc) {
    _igdbSvc = igdbSvc;
  }

  const long OverheadUnitsPerGame = 1024 * 1024;

  public override async Task<int> ExecuteAsync(CommandContext context, ImportSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return ConsoleHelper.Fail("--console is required");
    if (!Directory.Exists(settings.ReadPath)) return ConsoleHelper.Fail($"Path does not exist: {settings.ReadPath}");

    var files = GetFiles(settings);
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    await ConsoleHelper.Build(
      files,
      settings,
      totalWork: FileHelper.TotalCopyBytes(files) + OverheadUnitsPerGame * files.Count,
      maxConcurrency: 100,
      processFile: (file, name, displayName, task) => Import(file, name, displayName, settings, task, _igdbSvc),
      getNames: file => {
        var filePath = file.FullName;
        var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
        var displayName = fileNameNoExt.Replace("_", ":");
        return (fileNameNoExt, displayName);
      },
      displayPlatform: true,
      suffix: "Game"
    );

    return 0;
  }

  static async Task Import(
    FileInfo fileInfo,
    string name,
    string displayName,
    ImportSettings settings,
    ProgressTask progress,
    IgdbService igdbSvc
  ) {
    await igdbSvc
      .GetGame(displayName, settings.Console)
      .OnSuccessAsync(game => Success(fileInfo, game, name, settings, progress, igdbSvc))
      .OnNotFoundAsync(() => NotFound(fileInfo, displayName, progress));
  }

  private static async Task Success(
    FileInfo fileInfo,
    IgdbGame game,
    string name,
    ImportSettings settings,
    ProgressTask progress,
    IgdbService igdbSvc
  ) {
    var filePath = fileInfo.FullName;
    var fileSize = fileInfo.Length;
    var overheadRemaining = OverheadUnitsPerGame;
    var overheadStep = OverheadUnitsPerGame / 3;

    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    var media = await igdbSvc
      .GetMedia(game.Id)
      .OnNotFoundAsync(async () => ConsoleHelper.Warning($"Media not found for: '{game.Name}'")) 
      switch {
        Success<IgdbMedia> m => m.Value,
        _ => IgdbMedia.Empty
      };
    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    var gameCode = Encoder.Encode(game.Id);
    var gameFolderName = $"{gameCode} - {name}";
    var gameFolderPath = Path.Combine(settings.WritePath, gameFolderName);
    var regionFolderPath = Path.Combine(gameFolderPath, "regions", settings.Region);
    var versionsFolderPath = Path.Combine(regionFolderPath, "versions");
    var fileExtension = FileHelper.GetFileExtensionFromZip(filePath);

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

    // MetadataHelper.BuildAndWrite(fileInfo.Name, game, media.Cover, media.Screenshots, settings);

    MetadataHelper.BuildAndWrite(
      game.Name,
      game.Id,
      gameCode,
      settings.Console,
      game.Summary,
      fileExtension,
      media.Cover,
      media.Screenshots,
      gameFolderPath
    );

    if (overheadRemaining > 0) progress.Increment(overheadRemaining);
  }

  private static async Task NotFound(FileInfo fileInfo, string displayName, ProgressTask progress) {
    ConsoleHelper.Warning($"No IGDB match for: '{displayName}'");
    progress.Increment(fileInfo.Length + OverheadUnitsPerGame);
  }

  public List<FileInfo> GetFiles(ImportSettings settings ) {
    return Directory
      .GetFiles(settings.ReadPath, "*.zip*")
      .Where(f => string.IsNullOrEmpty(settings.Name) || Path.GetFileNameWithoutExtension(f) == settings.Name)
      .Select(f => new FileInfo(f))
      .ToList();
  }
}
