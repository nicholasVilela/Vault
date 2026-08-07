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
using Vault.Http;

namespace Vault.Commands;

public class GamelistCommand : AsyncCommand<GamelistSettings> {
  private readonly MessageService _messageSvc;
  private readonly HttpService _httpSvc;

  public GamelistCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
    _httpSvc = new HttpService(4, 1, 8);
  }

  public override async Task<int> ExecuteAsync(CommandContext context, GamelistSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return _messageSvc.Error("--console is required");
    if (!Directory.Exists(settings.ReadPath)) return _messageSvc.Error($"Path does not exist: {settings.ReadPath}");

    var files = GetFiles(settings);
    if (files.Count == 0) _messageSvc.Error($"No game files found in: '{settings.ReadPath}'");
    
    var imagePath = @$"{settings.DefaultDestination}/images";
    if (!settings.NoImages && !Directory.Exists(imagePath)) Directory.CreateDirectory(imagePath);

    var gameElements = new ConcurrentBag<XElement>();

    await ConsoleHelper.Build(
      files,
      settings,
      totalWork: files.Count,
      maxConcurrency: 100,
      processFile: (file, fileName, displayName, task) => Process(file, fileName, settings, task, gameElements),
      getNames: file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      },
      finalize: () => new XDocument(new XElement("gamelist", gameElements)).Save(@$"{settings.WritePath}/gamelist.xml"),
      displayPlatform: true,
      suffix: "Game",
      messageSvc: _messageSvc
    );

    return 0;
  }

  public async Task Process(
    FileInfo fileInfo,
    string fileName,
    GamelistSettings settings,
    ProgressTask progress,
    ConcurrentBag<XElement> elements
  ) {
    var metadata = MetadataHelper.Parse(fileInfo);
    
    if (!settings.NoImages) await DownloadImages(metadata, fileName, settings);

    progress.Increment(1);

    elements.Add(new XElement(
      "game",
        new XElement("path", $"./{fileName}{metadata.Extension}"),
        new XElement("name", metadata.Title),
        new XElement("desc", metadata.Summary),
        new XElement("image", $"./images/{fileName}.jpg")
      )
    );
  }

  private async Task DownloadImages(Metadata metadata, string name, GamelistSettings settings) {
    var url = $"https:{metadata.Media.Cover}";
    var imagesDir = $"{settings.WritePath}/images";
    var outputFile = $"{imagesDir}/{name}.jpg";

    using var response = await _httpSvc.GetAsync(url);
    response.EnsureSuccessStatusCode();

    await using var source = await response.Content.ReadAsStreamAsync();
    using var destination = File.Create(outputFile);

    await source.CopyToAsync(destination);
  }

  public List<FileInfo> GetFiles(GamelistSettings settings) {
    var result = new List<FileInfo>();

    foreach (var gameDir in Directory.EnumerateDirectories(settings.ReadPath)) {
      var gameName = GetGameName(Path.GetFileName(gameDir));

      if (!string.IsNullOrEmpty(settings.Name) && gameName != settings.Name) continue;

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

  public string GetGameName(string folderName) {
    var index = folderName.IndexOf(" - ", StringComparison.Ordinal);

    return index >= 0
      ? folderName[(index + 3)..]
      : folderName;
  }
}
