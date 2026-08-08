using Spectre.Console;
using Spectre.Console.Cli;
using Vault.Core.Message;
using Vault.Cli.Renderer;
using Vault.Cli.Files;
using Vault.Cli.Job;

namespace Vault.Cli.Commands;

public class ExportCommand : AsyncCommand<ExportSettings> {
  private readonly MessageService _messageSvc;

  public ExportCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }
  
  const long OverheadUnitsPerGame = 1024 * 1024;

  public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken _cancellationToken) {
    await new JobDispatcher<ExportSettings>()
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
      .GetProcess((file, name, displayName, task) => Process(file, name, settings, task))
      .GetWork(files => FileHelper.TotalCopyBytes(files) + (settings.Extract ? FileHelper.TotalExtractBytes(files) : 0) + OverheadUnitsPerGame * files.Count)
      .Run(_messageSvc);

    return 0;
  }

  private async Task<JobResult> Process(FileInfo file, string name, ExportSettings settings, ProgressTask task) {
    task.Increment(OverheadUnitsPerGame);

    Directory.CreateDirectory(settings.WritePath);
    
    var destPath = $"{settings.WritePath}/{name}.zip";
    await GetProgress(task, file.Length, progress => FileHelper.Copy(file.FullName, destPath, progress));
    if (settings.Extract) await GetProgress(task, FileHelper.ExtractBytes(file), progress => FileHelper.Extract(destPath, progress));

    return JobResult.SuccessResult;
  }

  public List<FileInfo> GetFiles(ExportSettings settings) {
    return Directory.EnumerateDirectories(settings.ReadPath)
      .Where(f => Path.GetFileName(f).Contains(" - "))
      .Select(f => new {
        Path = f,
        Name = SplitPath(f)
      })
      .Where(f => string.IsNullOrEmpty(settings.Name) || Path.GetFileNameWithoutExtension(f.Name) == settings.Name)
      .Select(f => Path.Combine(f.Path, "regions", settings.Region, "versions", $"{settings.Version}.zip"))
      .Where(f => File.Exists(f))
      .Select(f => new FileInfo(f))
      .ToList();
  }

  private string SplitPath(string value, int index = 4) {
    return value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[index].Split(" - ", 2)[1];
  }

  private static async Task GetProgress(ProgressTask task, long total, Func<IProgress<long>, Task> operation) {
    var lastReported = 0L;

    var progress = new Progress<long>(bytes => {
      if (bytes <= lastReported) return;

      var delta = bytes - lastReported;
      lastReported = bytes;
      task.Increment(delta);
    });

    await operation(progress);

    if (lastReported < total) task.Increment(total - lastReported);
  }
}
