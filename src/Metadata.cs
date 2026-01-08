using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vault;

public static class MetadataHelper {
  public static string Build(
    string title,
    int gameId,
    string gameCode,
    string platform,
    string coverUrl,
    List<string> screenshots) {
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

  public static async Task Write(string yaml, string metadataPath) {
    await File.WriteAllTextAsync(metadataPath, yaml);
  }

  public static async void BuildAndWrite(
    string title,
    int gameId,
    string gameCode,
    string platform,
    string coverUrl,
    List<string> screenshots,
    string gameFolderPath
  ) {
    var metadataPath = Path.Combine(gameFolderPath, "metadata.yaml");
    await Write(Build(title, gameId, gameCode, platform, coverUrl, screenshots), metadataPath);
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
  public MediaBlock Media { get; set; }
}

public sealed class MediaBlock {
  public string Cover { get; set; }
  public List<string> Screenshots { get; set; }
}
