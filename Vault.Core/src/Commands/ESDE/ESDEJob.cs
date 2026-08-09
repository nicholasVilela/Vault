using Vault.Core.Files;
using Vault.Core.Job;
using Vault.Core.Message;

namespace Vault.Core.Commands;

public static class ESDEJob {
  const int OverheadUnitsPerGame = 2;

  public static JobDispatcher<ESDEOptions> CreateJob(ESDEOptions options, MessageService messageSvc) {
    Directory.CreateDirectory($"{options.WritePath}/gamelists");
    Directory.CreateDirectory($"{options.WritePath}/downloaded_media");

    var job = new JobDispatcher<ESDEOptions>()
      .WithDispatcherOptions(new JobDispatcherOptions(100))
      .WithJobOptions(options)
      .GetFiles(_ => GetFiles(options))
      .GetNames(file => (file.FullName, GetConsoleName(file.Name.ToLower())))
      .GetProcess((file, fileName, displayName, advance) => Process(fileName, displayName, options, advance, messageSvc))
      .GetWork(files => files.Count * OverheadUnitsPerGame);

    return job;
  }

  public static async Task<JobResult> Process(
    string folderPath,
    string console,
    ESDEOptions options,
    Action<long> advance,
    MessageService messageSvc
  ) {
    if (!Directory.Exists(folderPath)) {
      messageSvc.Error($"Console does not exist: '{console}'");
      return JobResult.SkipResult;
    }

    var sourceGamelistPath = $"{folderPath}/gamelist.xml";
    var targetGamelistPath = $"{options.WritePath}/gamelists/{console}";
    Directory.CreateDirectory(targetGamelistPath);
    await FileHelper.Copy(sourceGamelistPath, $"{targetGamelistPath}/gamelist.xml");
    advance(1);

    var sourceImagesPath = $"{folderPath}/images";
    var targetImagesPath = $"{options.WritePath}/downloaded_media/{console}/covers";
    Directory.CreateDirectory(targetImagesPath);
    foreach (var imagePath in Directory.EnumerateFiles(sourceImagesPath)) await FileHelper.Copy(imagePath, $"{targetImagesPath}/{new FileInfo(imagePath).Name}");
    advance(1);

    return JobResult.SuccessResult;
  }

  public static List<FileInfo> GetFiles(ESDEOptions options) {
    if (string.IsNullOrEmpty(options.ConsoleCSV)) {
      return Directory.EnumerateDirectories($"{options.Drive}/consoles").Select(file => new FileInfo(file)).Where(file => Path.Exists($"{file.FullName}/gamelist.xml")).ToList();
    }

    var files = new List<FileInfo>();
    var consoles = options.ConsoleCSV.Split(",");
    foreach (var console in consoles) {
      var directory = $"{options.Drive}/consoles/{console.ToLower()}";
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
