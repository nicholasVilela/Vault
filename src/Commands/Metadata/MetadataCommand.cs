using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.Helpers;
using Vault.IGDB;

namespace Vault.Commands;

public class MetadataCommand : AsyncCommand<MetadataSettings> {
  private readonly IgdbService _igdbService;
  public MetadataCommand(IgdbService igdbService) {
    _igdbService = igdbService;
  }

  const int OverheadUnitsPerGame = 3;

  public override async Task<int> ExecuteAsync(CommandContext context, MetadataSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return ConsoleHelper.Fail("--console is required");

    var files = GetFiles(settings);
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    await ConsoleHelper.Build(
      files,
      settings,
      totalWork: files.Count * OverheadUnitsPerGame,
      maxConcurrency: 100,
      processFile: (file, fileName, displayName, task) => Process(fileName, displayName, settings, _igdbService, task),
      getNames: file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      }
    );

    return 0;
  }

  public async Task Process(
    string fileName,
    string displayName,
    MetadataSettings settings,
    IgdbService igdb,
    ProgressTask progress
  ) {
    var game = await igdb.GetGame(displayName, settings.Console);
    if (game == null) {
      ConsoleHelper.Warning($"No IGDB match for: '{displayName}'");
      progress.Increment(OverheadUnitsPerGame);
      return;
    }
    progress.Increment(1);

    var (cover, screenshots) = await igdb.GetMedia(game.Id);
    progress.Increment(1);

    MetadataHelper.BuildAndWrite(fileName, game, cover, screenshots, settings);
    progress.Increment(1);
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

  private string SplitPath(string value, int index = 3) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
  }
}
