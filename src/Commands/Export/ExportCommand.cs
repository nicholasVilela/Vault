using Spectre.Console;
using Spectre.Console.Cli;
using Vault.Message;
using Vault.Renderer;
using Vault.Files;

namespace Vault.Commands;

public class ExportCommand : AsyncCommand<ExportSettings> {
  private readonly MessageService _messageSvc;

  public ExportCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }
  
  const long OverheadUnitsPerGame = 1024 * 1024;

  public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken _cancellationToken) {
    // await ConsoleBuilder.Build(
    //   getFiles: _ => GetFiles(settings),
    //   settings,
    //   totalWork: files => FileHelper.TotalCopyBytes(files) + (settings.Extract ? FileHelper.TotalExtractBytes(files) : 0) + OverheadUnitsPerGame * files.Count,
    //   maxConcurrency: 100,
    //   processFile: (file, name, displayName, task) => Export(file, name, settings, task),
    //   getNames: file => {
    //     var filePath = file.FullName;
    //     var name = SplitPath(filePath);
    //     var displayName = name.Replace("_", ":");
    //     return (name, displayName);
    //   },
    //   displayPlatform: true,
    //   suffix: "Game",
    //   messageSvc: _messageSvc,
    //   validate: () => {
    //     if (string.IsNullOrWhiteSpace(settings.Console)) {
    //       _messageSvc.Error("Console is required with '-c' or '--console'");
    //       return false;
    //     }
    //     if (!Directory.Exists(settings.ReadPath)) {
    //       _messageSvc.Error($"Path does not exist: {settings.ReadPath}");
    //       return false;
    //     }

    //     return true;
    //   }
    // );

    return 0;
  }

  private async Task Export(FileInfo file, string name, ExportSettings settings, ProgressTask task) {
    task.Increment(OverheadUnitsPerGame);

    Directory.CreateDirectory(settings.WritePath);
    
    var destPath = $"{settings.WritePath}/{name}.zip";
    await GetProgress(task, file.Length, progress => FileHelper.Copy(file.FullName, destPath, progress));
    if (settings.Extract) await GetProgress(task, FileHelper.ExtractBytes(file), progress => FileHelper.Extract(destPath, progress));
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

  private string SplitPath(string value, int index = 3) {
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
