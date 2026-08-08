using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Cli;
using Vault.Message;
using Vault.Metadata.Data;
using Vault.Renderer;
using System.Xml.Linq;
using Vault.Http;
using Vault.Metadata;
using Vault.Job;

namespace Vault.Commands;

public class GamelistCommand : AsyncCommand<GamelistSettings> {
  private readonly MessageService _messageSvc;
  private readonly HttpService _httpSvc;

  public GamelistCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
    _httpSvc = new HttpService(4, 1, 8);
  }

  public override async Task<int> ExecuteAsync(CommandContext context, GamelistSettings settings, CancellationToken _cancellationToken) {    
    var imagePath = @$"{settings.DefaultDestination}/images";
    if (!settings.NoImages && !Directory.Exists(imagePath)) Directory.CreateDirectory(imagePath);

    var gameElements = new ConcurrentBag<XElement>();

    await new JobDispatcher<GamelistSettings>()
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
      .GetProcess((file, fileName, displayName, task) => Process(file, fileName, settings, task, gameElements))
      .GetWork(files => files.Count)
      .Finalize(() => new XDocument(new XElement("gamelist", gameElements)).Save(@$"{settings.WritePath}/gamelist.xml"))
      .Run(_messageSvc);

    return 0;
  }

  public async Task<JobResult> Process(
    FileInfo fileInfo,
    string fileName,
    GamelistSettings settings,
    ProgressTask progress,
    ConcurrentBag<XElement> elements
  ) {
    var metadata = MetadataBuilder.Parse(fileInfo);
    
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

    return JobResult.SuccessResult;
  }

  private async Task DownloadImages(GameMetadata metadata, string name, GamelistSettings settings) {
    if (string.IsNullOrEmpty(metadata.Media.Cover)) {
      _messageSvc.Warning($"No cover art found for: '{metadata.Title}'");
      return;
    }

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
