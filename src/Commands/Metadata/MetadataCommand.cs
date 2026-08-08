using Spectre.Console;
using Spectre.Console.Cli;
using Vault.Message;
using Vault.Renderer;
using Vault.IGDB;
using Vault.IGDB.Data;
using Vault.Metadata;
using Vault.Job;

namespace Vault.Commands;

public class MetadataCommand : AsyncCommand<MetadataSettings> {
  private readonly IgdbService _igdbSvc;
  private readonly MessageService _messageSvc;

  public MetadataCommand(IgdbService igdbSvc, MessageService messageSvc) {
    _igdbSvc = igdbSvc;
    _messageSvc = messageSvc;
  }

  const int OverheadUnitsPerGame = 3;

  public override async Task<int> ExecuteAsync(CommandContext context, MetadataSettings settings, CancellationToken _cancellationToken) {
    await new JobServiceBuilder<MetadataSettings>()
      .WithSettings(settings)
      .WithJobOptions(new JobOptions(100))
      .WithRenderOptions(new RenderOptions(true, "Game"))
      .Assert(() => string.IsNullOrEmpty(settings.Console), () => _messageSvc.Error("Console is required with '-c' or '--console'"))
      .Assert(() => !Directory.Exists(settings.ReadPath), () => _messageSvc.Error($"Path does not exist: '{settings.ReadPath}'"))
      .GetFiles(_ => GetFiles(settings))
      .GetNames(file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      })
      .GetProcess((file, fileName, displayName, task) => Process(fileName, displayName, settings, _igdbSvc, task))
      .GetWork(files => files.Count * OverheadUnitsPerGame)
      .Run(_messageSvc);

    return 0;
  }

  public async Task<JobResult> Process(
    string fileName,
    string displayName,
    MetadataSettings settings,
    IgdbService igdbSvc,
    ProgressTask progress
  ) {
    var result = await igdbSvc
      .GetGame(displayName, settings.Console, _messageSvc)
      .OnSuccessAsync(game => Success(fileName, game, settings, progress, igdbSvc))
      .OnNotFoundAsync(() => NotFound(displayName, progress));

    return result switch {
      Success<IgdbGame> => JobResult.SuccessResult,
      _ => JobResult.SkipResult
    };
  }

  private async Task Success(string fileName, IgdbGame game, MetadataSettings settings, ProgressTask progress, IgdbService igdbSvc) {
    progress.Increment(1);

    var media = await igdbSvc
      .GetMedia(game.Id)
      .OnNotFoundAsync(async () => _messageSvc.Warning($"Media not found for: '{game.Name}'")) 
      switch {
        Success<IgdbMedia> m => m.Value,
        _ => IgdbMedia.Empty
      };
    progress.Increment(1);

    MetadataBuilder.BuildAndWrite(fileName, game, media.Cover, media.Screenshots, settings, _messageSvc);
    progress.Increment(1);
  }

  private async Task NotFound(string displayName, ProgressTask progress) {
    _messageSvc.Warning($"No IGDB match for: {displayName}");
    progress.Increment(OverheadUnitsPerGame);
  }

  public List<FileInfo> GetFiles(MetadataSettings settings) {
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

  private string SplitPath(string value, int index = 4) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
  }
}
