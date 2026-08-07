using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.Message;
using Vault.Data;
using Vault.Helpers;
using Vault.IGDB;
using System.Xml.Linq;

namespace Vault.Commands;

public class GameListCommand : AsyncCommand<GameListSettings> {
  private readonly MessageService _messageSvc;

  public GameListCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, GameListSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return _messageSvc.Error("--console is required");
    if (!Directory.Exists(settings.ReadPath)) return _messageSvc.Error($"Path does not exist: {settings.ReadPath}");

    var files = GetFiles(settings);
    if (files.Count == 0) _messageSvc.Error($"No game files found in: '{settings.ReadPath}'");

    // using var writer = XmlWriter.Create(@$"{settings.DefaultDestination}/gamelist.xml", new XmlWriterSettings {
    //   Indent = true,
    //   OmitXmlDeclaration = true,
    //   ConformanceLevel = ConformanceLevel.Document
    // });
    // writer.WriteStartElement("gameList");

    

    var imagePath = @$"{settings.DefaultDestination}/images";
    if (!settings.NoImages && !Directory.Exists(imagePath)) Directory.CreateDirectory(imagePath);

    using var http = new HttpClient();
    var gameElements = new ConcurrentBag<XElement>();

    await ConsoleHelper.Build(
      files,
      settings,
      totalWork: files.Count,
      maxConcurrency: 100,
      processFile: (file, fileName, displayName, task) => Process(http, file, fileName, settings, task, gameElements),
      getNames: file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      },
      finalize: () => new XDocument(new XElement("gamelist", gameElements)).Save(@$"{settings.DefaultDestination}/gamelist.xml"),
      displayPlatform: true,
      suffix: "Game",
      messageSvc: _messageSvc
    );

    return 0;
  }

  public async Task<GameEntry> Process(
    HttpClient http,
    FileInfo fileInfo,
    string fileName,
    GameListSettings settings,
    ProgressTask progress,
    ConcurrentBag<XElement> elements
  ) {
    var metadata = MetadataHelper.Parse(fileInfo);
    
    if (!settings.NoImages) await DownloadImages(http, metadata, fileName, settings);

    progress.Increment(1);
    var entry = new GameEntry(fileName, metadata);

    var element = new XElement("game",
      new XElement("path", $"./{entry.Name}{entry.Metadata.Extension}"),
      new XElement("name", entry.Metadata.Title),
      new XElement("desc", entry.Metadata.Summary),
      new XElement("image", $"./images/{entry.Name}.jpg"));

    elements.Add(element);

    return entry;
  }

  private async Task DownloadImages(HttpClient http, Metadata metadata, string name, GameListSettings settings) {
    var url = "https:" + metadata.Media.Cover;
    
    var imagesDir = Path.Combine(settings.DefaultDestination, "images");

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

  private string SplitPath(string value, int index = 4) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
  }
}
