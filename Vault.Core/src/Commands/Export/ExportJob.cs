using Vault.Core.Message;
using Vault.Core.Files;
using Vault.Core.Job;

namespace Vault.Core.Commands;

public static class ExportJob {
  const long OverheadUnitsPerGame = 1024 * 1024;

  public static JobDispatcher<ExportOptions> CreateJob(ExportOptions options, MessageService messageSvc) {
    var job = new JobDispatcher<ExportOptions>()
      .WithDispatcherOptions(new JobDispatcherOptions(100))
      .WithJobOptions(options)
      .Assert(() => string.IsNullOrEmpty(options.Console), () => messageSvc.Error("Console is required with '-c' or '--console'"))
      .Assert(() => !Directory.Exists(options.ReadPath), () => messageSvc.Error($"Path does not exist: '{options.ReadPath}'"))
      .GetFiles(_ => GetFiles(options))
      .GetNames(file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      })
      .GetProcess((file, name, displayName, advance) => Process(file, name, options, advance))
      .GetWork(files => FileHelper.TotalCopyBytes(files) + (options.Extract ? FileHelper.TotalExtractBytes(files) : 0) + OverheadUnitsPerGame * files.Count);

    return job;
  }

  private static async Task<JobResult> Process(
    FileInfo file,
    string name,
    ExportOptions options,
    Action<long> advance
  ) {
    advance(OverheadUnitsPerGame);

    Directory.CreateDirectory(options.WritePath);
    
    var destPath = $"{options.WritePath}/{name}.zip";
    await GetProgress(advance, file.Length, progress => FileHelper.Copy(file.FullName, destPath, progress));
    if (options.Extract) await GetProgress(advance, FileHelper.ExtractBytes(file), progress => FileHelper.Extract(destPath, progress));

    return JobResult.SuccessResult;
  }

  public static List<FileInfo> GetFiles(ExportOptions options) {
    return Directory.EnumerateDirectories(options.ReadPath)
      .Where(f => Path.GetFileName(f).Contains(" - "))
      .Select(f => new {
        Path = f,
        Name = SplitPath(f)
      })
      .Where(f => string.IsNullOrEmpty(options.Name) || Path.GetFileNameWithoutExtension(f.Name) == options.Name)
      .Select(f => Path.Combine(f.Path, "regions", options.Region, "versions", $"{options.Version}.zip"))
      .Where(f => File.Exists(f))
      .Select(f => new FileInfo(f))
      .ToList();
  }

  private static string SplitPath(string value, int index = 4) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
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
