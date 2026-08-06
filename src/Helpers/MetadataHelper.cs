using System.Text;
using Spectre.Console;
using Vault.Commands;
using Vault.Data;
using Vault.IGDB.Data;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vault.Helpers;

public static class MetadataHelper {
  public static string Build(
    string title,
    int gameId,
    string gameCode,
    string platform,
    string summary,
    string extension,
    string coverUrl,
    List<string> screenshots
    ) {
    var meta = new Metadata {
      Title = title.Replace("'", "''"),
      GameId = gameId,
      GameCode = gameCode,
      Platform = platform,
      Summary = summary,
      Extension = extension,
      Media = new Metadata.MediaBlock {
        Cover = coverUrl,
        Screenshots = screenshots,
      },
    };

    var serializer = new SerializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

    var yaml = serializer.Serialize(meta);
    return yaml;
  }

  public static async Task Write(string yaml, string metadataPath) {
    await File.WriteAllTextAsync(metadataPath, yaml);
  }

  public static async void BuildAndWrite(
    string title,
    int gameId,
    string gameCode,
    string platform,
    string summary,
    string coverUrl,
    string extension,
    List<string> screenshots,
    string gameFolderPath
  ) {
    var metadataPath = Path.Combine(gameFolderPath, "metadata.yaml");
    await Write(Build(title, gameId, gameCode, platform, summary, coverUrl, extension, screenshots), metadataPath);
  }

  public static Metadata Parse(FileInfo file) {
    using var reader = file.OpenText();

    var deserializer = new DeserializerBuilder()
      .WithNamingConvention(UnderscoredNamingConvention.Instance)
      .IgnoreUnmatchedProperties()
      .Build();

    return deserializer.Deserialize<Metadata>(reader);
  }

  public static void BuildAndWrite(string fileName, IgdbGame game, string cover, List<string> screenshots, BaseSettings settings) {
    var gameCode = Encoder.Encode(game.Id);
    var gameFolderName = $"{gameCode} - {fileName}";
    var gameFolderPath = Path.Combine(settings.WritePath, gameFolderName);
    if (!Path.Exists(gameFolderPath)) {
      ConsoleHelper.Warning($"No path found for: '{gameFolderPath}'");
      return;
    }
    DeleteExistingMetadataFiles(gameFolderPath);

    var regionFolderPath = Path.Combine(gameFolderPath, "regions", settings.Region);
    var versionsFolderPath = Path.Combine(regionFolderPath, "versions");
    var filePath = Path.Combine(versionsFolderPath, $"{settings.Version}.zip");

    var fileExtension = FileHelper.GetFileExtensionFromZip(filePath);
    if (fileExtension == null) {
      ConsoleHelper.Warning($"File does not exist: '{filePath}'");
      return;
    }

    BuildAndWrite(
      game.Name,
      game.Id,
      gameCode,
      settings.Console,
      game.Summary,
      fileExtension,
      cover,
      screenshots,
      gameFolderPath
    );
  }

  static void DeleteExistingMetadataFiles(string gameFolderPath) {
    var yaml = Path.Combine(gameFolderPath, "metadata.yaml");
    var yml  = Path.Combine(gameFolderPath, "metadata.yml");

    try { if (File.Exists(yaml)) File.Delete(yaml); } catch { }
    try { if (File.Exists(yml))  File.Delete(yml);  } catch { }
  }
}
