using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.IGDB;

namespace Vault.Commands;

public class GameListCommand : AsyncCommand<GameListSettings> {
  public override async Task<int> ExecuteAsync(CommandContext context, GameListSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return ConsoleHelper.Fail("--console is required");

    if (!Directory.Exists(settings.ReadPath)) return ConsoleHelper.Fail($"Path does not exist: {settings.ReadPath}");

    var clientId = Environment.GetEnvironmentVariable("IGDB_CLIENT_ID");
    if (string.IsNullOrWhiteSpace(clientId)) return ConsoleHelper.Fail("Missing IGDB_CLIENT_ID environment variable.");

    var clientSecret = Environment.GetEnvironmentVariable("IGDB_CLIENT_SECRET");
    if (string.IsNullOrWhiteSpace(clientSecret)) return ConsoleHelper.Fail("Missing IGDB_CLIENT_SECRET environment variable.");

    var files = GetFiles(settings);
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    using var igdb = new IgdbService(clientId, clientSecret);

    var xmlSettings = new XmlWriterSettings {
      Indent = true,
      OmitXmlDeclaration = true,
      ConformanceLevel = ConformanceLevel.Document
    };
    using var writer = XmlWriter.Create(@$"{settings.DefaultDestination}\gamelist.xml", xmlSettings);
    writer.WriteStartElement("gameList");

    var imagePath = @$"{settings.DefaultDestination}\IMAGES";
    if (!settings.NoImages && !Directory.Exists(imagePath)) Directory.CreateDirectory(imagePath);

    using var http = new HttpClient();

    await ConsoleHelper.Build(
      files,
      settings,
      totalWork: files.Count,
      maxConcurrency: 100,
      processFile: (file, fileName, displayName, task) => Process(http, file, fileName, settings, task),
      getNames: file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      },
      finalize: entries => {
        foreach (var entry in entries) {
          writer.WriteStartElement("game");
          writer.WriteElementString("path", $"./{entry.Name}.gba");
          writer.WriteElementString("name", entry.Metadata.Title);
          writer.WriteElementString("desc", entry.Metadata.Summary);
          writer.WriteElementString("image", $"./IMAGES/{entry.Name}.jpg");
          writer.WriteEndElement();
        }
        writer.WriteEndElement();
        return Task.CompletedTask;
      }
    );

    return 0;
  }

  public async Task<GameEntry> Process(
    HttpClient http,
    FileInfo fileInfo,
    string fileName,
    GameListSettings settings,
    ProgressTask progress
  ) {
    var metadata = MetadataHelper.Parse(fileInfo);
    
    if (!settings.NoImages) await DownloadImages(http, metadata, fileName, settings);

    progress.Increment(1);
    return new GameEntry(fileName, metadata);
  }

  private async Task DownloadImages(HttpClient http, Metadata metadata, string name, GameListSettings settings) {
    var url = "https:" + metadata.Media.Cover;
    
    var imagesDir = Path.Combine(settings.DefaultDestination, "IMAGES");

    var outputFile = Path.Combine(imagesDir, $"{name}.jpg");

    using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    await using var src = await response.Content.ReadAsStreamAsync();
    await using var dst = File.Create(outputFile);

    await src.CopyToAsync(dst);
  }

  public List<FileInfo> GetFiles(GameListSettings settings) {
    var result = new List<FileInfo>();

    foreach (var gameDir in Directory.EnumerateDirectories(settings.ReadPath)) {
      var gameName = Path.GetFileName(gameDir);

      if (!string.IsNullOrEmpty(settings.Name) &&
          !string.Equals(gameName, settings.Name, StringComparison.OrdinalIgnoreCase))
        continue;

      var metaPath = GetMetadataPath(gameDir);
      if (metaPath == null) continue;

      result.Add(new FileInfo(metaPath));
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

  private string SplitPath(string value, int index = 3) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
  }
}
