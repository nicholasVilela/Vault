using System.Text;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vault;

public static class MetadataHelper {
  public static string Build(
    string title,
    int gameId,
    string gameCode,
    string platform,
    string summary,
    string coverUrl,
    List<string> screenshots
    ) {
    var meta = new Metadata {
      Title = title.Replace("'", "''"),
      GameId = gameId,
      GameCode = gameCode,
      Platform = platform,
      Summary = summary,
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
    List<string> screenshots,
    string gameFolderPath
  ) {
    if (!Path.Exists(gameFolderPath)) {
      AnsiConsole.MarkupLine($"[yellow]No path found for:[/] {gameFolderPath}");
      return;
    }
    var metadataPath = Path.Combine(gameFolderPath, "metadata.yaml");
    await Write(Build(title, gameId, gameCode, platform, summary, coverUrl, screenshots), metadataPath);
  }

  public static Metadata Parse(FileInfo file) {
    using var reader = file.OpenText();

  var deserializer = new DeserializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .IgnoreUnmatchedProperties()
    .Build();

  return deserializer.Deserialize<Metadata>(reader);
  }
}

public class Metadata {
  public string Title { get; set; }
  public int GameId { get; set; }
  public string GameCode { get; set; }
  public string Platform { get; set; }
  public string Summary { get; set; }
  public MediaBlock Media { get; set; }

  public class MediaBlock {
    public string Cover { get; set; }
    public List<string> Screenshots { get; set; }
  }
}
