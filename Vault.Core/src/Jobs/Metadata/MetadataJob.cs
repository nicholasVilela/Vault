using Vault.Core.IGDB;
using Vault.Core.IGDB.Data;
using Vault.Core.Message;

namespace Vault.Core.Jobs;

public static class MetadataJob {
  const int OverheadUnitsPerGame = 3;

  public static JobRunner<IMetadataSettings> Create(IMetadataSettings settings, IgdbService igdbSvc, MessageService messageSvc) {
    var job = new JobRunner<IMetadataSettings>()
      .WithDispatcherOptions(new JobRunnerOptions(100))
      .WithJobOptions(settings)
      .Assert(() => string.IsNullOrEmpty(settings.Console), () => messageSvc.Error("Console is required with '-c' or '--console'"))
      .Assert(() => !Directory.Exists(settings.ReadPath), () => messageSvc.Error($"Path does not exist: '{settings.ReadPath}'"))
      .GetFiles(_ => GetFiles(settings))
      .GetNames(file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      })
      .GetProcess((file, fileName, displayName, advance) => Process(fileName, displayName, settings, igdbSvc, messageSvc, advance))
      .GetWork(files => files.Count * OverheadUnitsPerGame);

    return job;
  }

  public static async Task<JobResult> Process(
    string fileName,
    string displayName,
    IMetadataSettings settings,
    IgdbService igdbSvc,
    MessageService messageSvc,
    Action<long> advance
  ) {
    var result = await igdbSvc
      .GetGame(displayName, settings.Console, messageSvc)
      .OnSuccessAsync(game => Success(fileName, game, settings, advance, igdbSvc, messageSvc))
      .OnNotFoundAsync(() => NotFound(displayName, advance, messageSvc));

    return result switch {
      Success<IgdbGame> => JobResult.SuccessResult,
      _ => JobResult.SkipResult
    };
  }

  private static async Task Success(string fileName, IgdbGame game, IMetadataSettings settings, Action<long> advance, IgdbService igdbSvc, MessageService messageSvc) {
    advance(1);

    var media = await igdbSvc
      .GetMedia(game.Id)
      .OnNotFoundAsync(async () => messageSvc.Warning($"Media not found for: '{game.Name}'")) 
      switch {
        Success<IgdbMedia> m => m.Value,
        _ => IgdbMedia.Empty
      };
    advance(1);

    MetadataBuilder.BuildAndWrite(fileName, game, media.Cover, media.Screenshots, settings, messageSvc);
    advance(1);
  }

  private static async Task NotFound(string displayName, Action<long> advance, MessageService messageSvc) {
    messageSvc.Warning($"No IGDB match for: {displayName}");
    advance(OverheadUnitsPerGame);
  }

  public static List<FileInfo> GetFiles(IMetadataSettings settings) {
    var result = new List<FileInfo>();

    foreach (var gameDir in Directory.EnumerateDirectories(settings.ReadPath)) {
      var gameName = Path.GetFileName(gameDir);

      var sepIndex = gameName.IndexOf(" - ", StringComparison.Ordinal);
      if (sepIndex <= 0 || sepIndex + 3 >= gameName.Length)
        continue;

      var name = gameName[(sepIndex + 3)..];

      if (!string.IsNullOrEmpty(settings.Name) &&
          !string.Equals(name, settings.Name, StringComparison.OrdinalIgnoreCase))
        continue;

      result.Add(new FileInfo(gameDir));
    }

    return result;
  }

  private static string SplitPath(string value, int index = 4) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
  }
}
