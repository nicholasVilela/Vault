using System.Collections.Concurrent;
using Vault.Core.Message;
using System.Xml.Linq;
using Vault.Core.Http;
using Vault.Core.Job;

namespace Vault.Core.Commands;

public static class GamelistJob {
  public static JobDispatcher<GamelistOptions> CreateJob(GamelistOptions settings, MessageService messageSvc, HttpService httpSvc) {    
    var imagePath = @$"{settings.DefaultDestination}/images";
    if (!settings.NoImages && !Directory.Exists(imagePath)) Directory.CreateDirectory(imagePath);

    var gameElements = new ConcurrentBag<XElement>();

    var job =  new JobDispatcher<GamelistOptions>()
      .WithDispatcherOptions(new JobDispatcherOptions(100))
      .WithJobOptions(settings)
      .Assert(() => string.IsNullOrEmpty(settings.Console), () => messageSvc.Error("Console is required with '-c' or '--console'"))
      .Assert(() => !Directory.Exists(settings.ReadPath), () => messageSvc.Error($"Path does not exist: '{settings.ReadPath}'"))
      .GetFiles(_ => GetFiles(settings))
      .GetNames(file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      })
      .GetProcess((file, fileName, displayName, task) => Process(file, fileName, settings, task, gameElements, messageSvc, httpSvc))
      .GetWork(files => files.Count)
      .Finalize(() => new XDocument(new XElement("gamelist", gameElements)).Save(@$"{settings.WritePath}/gamelist.xml"));

    return job;
  }

  public static async Task<JobResult> Process(
    FileInfo fileInfo,
    string fileName,
    GamelistOptions settings,
    Action<long> advance,
    ConcurrentBag<XElement> elements,
    MessageService messageSvc,
    HttpService httpSvc
  ) {
    var metadata = MetadataBuilder.Parse(fileInfo);
    
    if (!settings.NoImages) await DownloadImages(metadata, fileName, settings, messageSvc, httpSvc);

    advance(1);

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

  private static async Task DownloadImages(GameMetadata metadata, string name, GamelistOptions settings, MessageService messageSvc, HttpService httpSvc) {
    if (string.IsNullOrEmpty(metadata.Media.Cover)) {
      messageSvc.Warning($"No cover art found for: '{metadata.Title}'");
      return;
    }

    var url = $"https:{metadata.Media.Cover}";
    var imagesDir = $"{settings.WritePath}/images";
    var outputFile = $"{imagesDir}/{name}.jpg";

    using var response = await httpSvc.GetAsync(url);
    response.EnsureSuccessStatusCode();

    await using var source = await response.Content.ReadAsStreamAsync();
    using var destination = File.Create(outputFile);

    await source.CopyToAsync(destination);
  }

  public static List<FileInfo> GetFiles(GamelistOptions settings) {
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

  private static string SplitPath(string value, int index = 4) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
  }

  public static string GetGameName(string folderName) {
    var index = folderName.IndexOf(" - ", StringComparison.Ordinal);

    return index >= 0
      ? folderName[(index + 3)..]
      : folderName;
  }
}
