using System.Collections.Concurrent;
using System.IO.Compression;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace Vault.Commands;

public class ExportCommand : AsyncCommand<ExportSettings> {
  const long OverheadUnitsPerGame = 1024 * 1024;

  public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken _cancellationToken) {
    if (string.IsNullOrWhiteSpace(settings.Console)) return ConsoleHelper.Fail("--console is required");

    if (!Directory.Exists(settings.ReadPath)) return ConsoleHelper.Fail($"Path does not exist: {settings.ReadPath}");

    var files = GetFiles(settings);
    if (files.Count == 0) return ConsoleHelper.Warning($"No game files found in: {settings.ReadPath}");

    await ConsoleHelper.Build(
      files,
      settings,
      totalWork: FileHelper.TotalCopyBytes(files) + OverheadUnitsPerGame + (settings.Extract ? FileHelper.TotalExtractBytes(files) : 0),
      maxConcurrency: 100,
      processFile: (file, name, displayName, s, task) => Export(file, name, s, task),
      getNames: file => {
        var filePath = file.FullName;
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = parts[2].Split(" - ", 2)[1];
        var displayName = name.Replace("_", ":");
        return (name, displayName);
      }
    );

    return 0;
  }

  private async Task Export(FileInfo file, string name, ExportSettings settings, ProgressTask progress) {
    Directory.CreateDirectory(settings.WritePath);
    
    var destPath = $"{settings.WritePath}/{name}.zip";

    var fileSize = file.Length;

    var overheadRemaining = OverheadUnitsPerGame;
    var overheadStep = OverheadUnitsPerGame / 2;

    progress.Increment(overheadStep);
    overheadRemaining -= overheadStep;

    var copiedBytes = 0L;
    var copyProgress = new Progress<long>(bytes => {
      if (bytes <= 0) return;
      copiedBytes += bytes;
      progress.Increment(bytes);
    });
    await FileHelper.Copy(file.FullName, destPath, copyProgress);
    if (copiedBytes < fileSize) {
      progress.Increment(fileSize - copiedBytes);
    }


    if (settings.Extract) {
      var extractedBytes = 0L;
      var extractProgress = new Progress<long>(bytes => {
        if (bytes <= 0) return;
        extractedBytes += bytes;
        progress.Increment(bytes);
      });
      FileHelper.Extract(destPath, extractProgress);

      var totalExtractBytes = FileHelper.ExtractBytes(file);
      if (extractedBytes < totalExtractBytes) {
        progress.Increment(totalExtractBytes - extractedBytes);
      }
    }

    if (overheadRemaining > 0) {
      progress.Increment(overheadRemaining);
    }
  }

  public List<FileInfo> GetFiles(ExportSettings settings) {
    return Directory.EnumerateDirectories(settings.ReadPath)
      .Where(f => Path.GetFileName(f).Contains(" - "))
      .Select(f => new {
        Path = f,
        Name = f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[2].Split(" - ", 2)[1]
      })
      .Where(f => string.IsNullOrEmpty(settings.Name) || Path.GetFileNameWithoutExtension(f.Name) == settings.Name)
      .Select(f => Path.Combine(f.Path, "regions", settings.Region, "versions", $"{settings.Version}.zip"))
      .Where(f => File.Exists(f))
      .Select(f => new FileInfo(f))
      .ToList();
  }
}
