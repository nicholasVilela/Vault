using Vault.Core.Message;
using Vault.Core.IGDB;
using Vault.Core.IGDB.Data;
using Vault.Core.Files;
using Vault.Core.Encode;

namespace Vault.Core.Jobs;

public static class ImportJob {
  const long OverheadUnitsPerGame = 1024 * 1024;

  public static JobRunner<IImportSettings> Create(IImportSettings settings, IgdbService igdbSvc, MessageService messageSvc) {
    var job = new JobRunner<IImportSettings>()
      .WithRunnerSettings(new JobRunnerSettings(100))
      .WithJobSettings(settings)
      .Assert(() => string.IsNullOrEmpty(settings.Console), () => messageSvc.Error("Console is required with '-c' or '--console'"))
      .Assert(() => !Directory.Exists(settings.ReadPath), () => messageSvc.Error($"Path does not exist: '{settings.ReadPath}'"))
      .GetFiles(_ => GetFiles(settings))
      .GetNames(file => {
        var filePath = file.FullName;
        var fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
        var displayName = fileNameNoExt.Replace("_", ":");
        return (fileNameNoExt, displayName);
      })
      .GetProcess((file, name, displayName, advance) => Process(file, name, displayName, settings, advance, igdbSvc, messageSvc))
      .GetWork(files => FileHelper.TotalCopyBytes(files) + OverheadUnitsPerGame * files.Count);

    return job;
  }

  static async Task<JobResult> Process(
    FileInfo fileInfo,
    string name,
    string displayName,
    IImportSettings settings,
    Action<long> advance,
    IgdbService igdbSvc,
    MessageService messageSvc
  ) {
    var result = await igdbSvc
      .GetGame(displayName, settings.Console, messageSvc)
      .OnSuccessAsync(game => Success(fileInfo, game, name, settings, advance, igdbSvc, messageSvc))
      .OnNotFoundAsync(() => NotFound(fileInfo, displayName, advance, messageSvc));

    return result switch {
      Success<IgdbGame> s => JobResult.SuccessResult,
      _ => JobResult.SkipResult
    };
  }

  private static async Task Success(
    FileInfo fileInfo,
    IgdbGame game,
    string name,
    IImportSettings settings,
    Action<long> advance,
    IgdbService igdbSvc,
    MessageService messageSvc
  ) {
    var filePath = fileInfo.FullName;
    var fileSize = fileInfo.Length;
    var overheadRemaining = OverheadUnitsPerGame;
    var overheadStep = OverheadUnitsPerGame / 3;

    advance(overheadStep);
    overheadRemaining -= overheadStep;

    var media = await igdbSvc
      .GetMedia(game.Id)
      .OnNotFoundAsync(async () => messageSvc.Warning($"Media not found for: '{game.Name}'")) 
      switch {
        Success<IgdbMedia> m => m.Value,
        _ => IgdbMedia.Empty
      };
    advance(overheadStep);
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
      advance(bytes);
    });

    if (settings.Move) FileHelper.Move(filePath, versionFilePath, copyProgress);
    else await FileHelper.Copy(filePath, versionFilePath, copyProgress);

    if (copiedForThisFile < fileSize) {
      advance(fileSize - copiedForThisFile);
    }

    // MetadataBuilder.BuildAndWrite(fileInfo.Name, game, media.Cover, media.Screenshots, settings);

    MetadataBuilder.BuildAndWrite(
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

    if (overheadRemaining > 0) advance(overheadRemaining);
  }

  private static async Task NotFound(FileInfo fileInfo, string displayName, Action<long> advance, MessageService messageSvc) {
    messageSvc.Warning($"No IGDB match for: '{displayName}'");
    advance(fileInfo.Length + OverheadUnitsPerGame);
  }

  public static List<FileInfo> GetFiles(IImportSettings settings) {
    return Directory
      .GetFiles(settings.ReadPath, "*.zip*")
      .Where(f => string.IsNullOrEmpty(settings.Name) || Path.GetFileNameWithoutExtension(f) == settings.Name)
      .Select(f => new FileInfo(f))
      .ToList();
  }
}
