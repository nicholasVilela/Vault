using System.Collections.Concurrent;
using System.IO.Compression;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.Message;
using Vault.Helpers;

namespace Vault.Commands;

public class ExportCommand : AsyncCommand<ExportSettings> {
  private readonly MessageService _messageSvc;

  public ExportCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }
  
  const long OverheadUnitsPerGame = 1024 * 1024;

  public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken _cancellationToken) {
    await ConsoleHelper.Build(
      getFiles: _ => GetFiles(settings),
      settings,
      totalWork: files => FileHelper.TotalCopyBytes(files) + (settings.Extract ? FileHelper.TotalExtractBytes(files) : 0) + OverheadUnitsPerGame * files.Count,
      maxConcurrency: 100,
      processFile: (file, name, displayName, task) => Export(file, name, settings, task),
      getNames: file => {
        var filePath = file.FullName;
        var name = SplitPath(filePath);
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      },
      displayPlatform: true,
      suffix: "Game",
      messageSvc: _messageSvc,
      validate: () => {
        if (string.IsNullOrWhiteSpace(settings.Console)) {
          _messageSvc.Error("Console is required with '-c' or '--console'");
          return false;
        }
        if (!Directory.Exists(settings.ReadPath)) {
          _messageSvc.Error($"Path does not exist: {settings.ReadPath}");
          return false;
        }

        return true;
      }
    );

    return 0;
  }

  private async Task Export(FileInfo file, string name, ExportSettings settings, ProgressTask task) {
    task.Increment(OverheadUnitsPerGame);

    Directory.CreateDirectory(settings.WritePath);
    
    var destPath = $"{settings.WritePath}/{name}.zip";
    await ProgressHelper.Build(task, file.Length, progress => FileHelper.Copy(file.FullName, destPath, progress));
    if (settings.Extract) await ProgressHelper.Build(task, FileHelper.ExtractBytes(file), progress => FileHelper.Extract(destPath, progress));
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
}
