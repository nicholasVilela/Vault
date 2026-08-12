using Vault.Core.Message;
using Vault.Core.Files;

namespace Vault.Core.Jobs;

public static class ExportJob {
  const long OverheadUnitsPerGame = 1024 * 1024;

  public static JobRunner<IExportSettings> Create(IExportSettings settings, MessageService messageSvc) {
    var job = new JobRunner<IExportSettings>()
      .WithRunnerSettings(new JobRunnerSettings(100))
      .WithJobSettings(settings)
      .Assert(() => string.IsNullOrEmpty(settings.Console), () => messageSvc.Error("Console is required with '-c' or '--console'"))
      .Assert(() => !Directory.Exists(settings.ReadPath), () => messageSvc.Error($"Path does not exist: '{settings.ReadPath}'"))
      .GetFiles(_ => GetFiles(settings))
      .GetNames(file => {
        var filePath = file.FullName;
        var name = FileHelper.GetName(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      })
      .GetProcess((file, name, displayName, advance) => Process(file, name, settings, advance))
      .GetWork(files => FileHelper.TotalCopyBytes(files) + (settings.Extract ? FileHelper.TotalExtractBytes(files) : 0) + OverheadUnitsPerGame * files.Count);

    return job;
  }

  private static async Task<JobResult> Process(
    FileInfo file,
    string name,
    IExportSettings settings,
    Action<long> advance
  ) {
    advance(OverheadUnitsPerGame);

    Directory.CreateDirectory(settings.WritePath);
    
    var destPath = $"{settings.WritePath}/{name}.zip";
    await GetProgress(advance, file.Length, progress => FileHelper.Copy(file.FullName, destPath, progress));
    if (settings.Extract) await GetProgress(advance, FileHelper.ExtractBytes(file), progress => FileHelper.Extract(destPath, progress));

    return JobResult.SuccessResult;
  }

  public static List<FileInfo> GetFiles(IExportSettings settings) {
    return Directory.EnumerateDirectories(settings.ReadPath)
      .Where(f => Path.GetFileName(f).Contains(" - "))
      .Select(f => new {
        Path = f,
        Name = FileHelper.GetName(f)
      })
      .Where(f => string.IsNullOrEmpty(settings.Name) || Path.GetFileNameWithoutExtension(f.Name) == settings.Name)
      .Select(f => Path.Combine(f.Path, "regions", settings.Region, "versions", $"{settings.Version}.zip"))
      .Where(f => File.Exists(f))
      .Select(f => new FileInfo(f))
      .ToList();
  }

  private static async Task GetProgress(Action<long> advance, long total, Func<IProgress<long>, Task> operation) {
    var lastReported = 0L;

    var progress = new Progress<long>(bytes => {
      if (bytes <= lastReported) return;

      var delta = bytes - lastReported;
      lastReported = bytes;
      advance(delta);
    });

    await operation(progress);

    if (lastReported < total) advance(total - lastReported);
  }
}
