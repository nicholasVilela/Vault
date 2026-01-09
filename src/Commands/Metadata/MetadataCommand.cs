using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.IGDB;

namespace Vault.Commands;

public class MetadataCommand : AsyncCommand<MetadataSettings> {
  const int OverheadUnitsPerGame = 3;

  public override async Task<int> ExecuteAsync(CommandContext context, MetadataSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return ConsoleHelper.Fail("--console is required");

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
      totalWork: files.Count * OverheadUnitsPerGame,
      maxConcurrency: 100,
      processFile: (file, fileName, displayName, task) => Process(fileName, displayName, settings, igdb, task),
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
    var game = await igdb.SearchGameAsync(displayName, settings.Console);
    if (game == null) {
      AnsiConsole.MarkupLine($"[yellow]No IGDB match for:[/] {displayName}");
      progress.Increment(OverheadUnitsPerGame);
      return;
    }
    progress.Increment(1);

    var (cover, screenshots) = await igdb.GetMediaAsync(game.Id);
    progress.Increment(1);

    var gameCode = Encoder.Encode(game.Id);
    var gameFolderName = gameCode + " - " + fileName;
    var gameFolderPath = Path.Combine(settings.WritePath, gameFolderName);

    DeleteExistingMetadataFiles(gameFolderPath);

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

  static string GetMetadataPath(string gameDir) {
    var yaml = Path.Combine(gameDir, "metadata.yaml");
    if (File.Exists(yaml)) return yaml;

    var yml  = Path.Combine(gameDir, "metadata.yml");
    if (File.Exists(yml))  return yml;

    return null;
  }

  static void DeleteExistingMetadataFiles(string gameFolderPath) {
    var yaml = Path.Combine(gameFolderPath, "metadata.yaml");
    var yml  = Path.Combine(gameFolderPath, "metadata.yml");

    try { if (File.Exists(yaml)) File.Delete(yaml); } catch { }
    try { if (File.Exists(yml))  File.Delete(yml);  } catch { }
  }

  private string SplitPath(string value, int index = 3) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
  }
}
