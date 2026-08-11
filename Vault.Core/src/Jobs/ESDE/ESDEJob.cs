using Vault.Core.Files;
using Vault.Core.Message;

namespace Vault.Core.Jobs;

public static class ESDEJob {
  const int OverheadUnitsPerGame = 2;

  public static JobRunner<IESDESettings> Create(IESDESettings settings, MessageService messageSvc) {
    Directory.CreateDirectory($"{settings.WritePath}/gamelists");
    Directory.CreateDirectory($"{settings.WritePath}/downloaded_media");

    var job = new JobRunner<IESDESettings>()
      .WithRunnerSettings(new JobRunnerSettings(100))
      .WithJobSettings(settings)
      .GetFiles(_ => GetFiles(settings))
      .GetNames(file => (file.FullName, GetConsoleName(file.Name.ToLower())))
      .GetProcess((file, fileName, displayName, advance) => Process(fileName, displayName, settings, advance, messageSvc))
      .GetWork(files => files.Count * OverheadUnitsPerGame);

    return job;
  }

  public static async Task<JobResult> Process(
    string folderPath,
    string console,
    IESDESettings settings,
    Action<long> advance,
    MessageService messageSvc
  ) {
    if (!Directory.Exists(folderPath)) {
      messageSvc.Error($"Console does not exist: '{console}'");
      return JobResult.SkipResult;
    }

    var sourceGamelistPath = $"{folderPath}/gamelist.xml";
    var targetGamelistPath = $"{settings.WritePath}/gamelists/{console}";
    Directory.CreateDirectory(targetGamelistPath);
    await FileHelper.Copy(sourceGamelistPath, $"{targetGamelistPath}/gamelist.xml");
    advance(1);

    var sourceImagesPath = $"{folderPath}/images";
    var targetImagesPath = $"{settings.WritePath}/downloaded_media/{console}/covers";
    Directory.CreateDirectory(targetImagesPath);
    foreach (var imagePath in Directory.EnumerateFiles(sourceImagesPath)) await FileHelper.Copy(imagePath, $"{targetImagesPath}/{new FileInfo(imagePath).Name}");
    advance(1);

    return JobResult.SuccessResult;
  }

  public static List<FileInfo> GetFiles(IESDESettings settings) {
    if (string.IsNullOrEmpty(settings.ConsoleCSV)) {
      return Directory.EnumerateDirectories($"{settings.Drive}/consoles").Select(file => new FileInfo(file)).Where(file => Path.Exists($"{file.FullName}/gamelist.xml")).ToList();
    }

    var files = new List<FileInfo>();
    var consoles = settings.ConsoleCSV.Split(",");
    foreach (var console in consoles) {
      var directory = $"{settings.Drive}/consoles/{console.ToLower()}";
      files.Add(new FileInfo(directory));
    }

    return files.Where(file => {
      return Path.Exists($"{file.FullName}/gamelist.xml");
    }).ToList();
  }

  public static string GetConsoleName(string name) {
    return name switch {
      "3ds" => "n3ds",
      _ => name
    };
  }
}
