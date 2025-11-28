using System.IO.Compression;
using Spectre.Console;

namespace Vault;

public static class FileHelper {
  public static async Task Copy(
    string sourcePath,
    string destPath,
    IProgress<long> progress = null
  ) {
    const int bufferSize = 81920;
    var buffer = new byte[bufferSize];

    await using var source = new FileStream(
      sourcePath,
      FileMode.Open,
      FileAccess.Read,
      FileShare.Read
    );

    await using var dest = new FileStream(
      destPath,
      FileMode.Create,
      FileAccess.Write,
      FileShare.Read
    );

    while (true) {
      var read = await source.ReadAsync(buffer, 0, buffer.Length);
      if (read == 0) break;

      await dest.WriteAsync(buffer, 0, read);
      progress?.Report(read);
    }
  }

  public static async Task Extract(string zipPath, IProgress<long> progress = null) {
    var outputDir = Path.GetDirectoryName(zipPath)!;
    var baseName  = Path.GetFileNameWithoutExtension(zipPath);
    string destPath;

    using var archive = ZipFile.OpenRead(zipPath);

    var entry = archive.Entries
      .FirstOrDefault(e =>
        !string.IsNullOrWhiteSpace(e.Name) &&
        !e.FullName.EndsWith("/") &&
        !e.FullName.EndsWith(@"\")
      );

    if (entry == null) throw new InvalidOperationException($"ZIP '{zipPath}' contains no files.");

    destPath = Path.Combine(
        outputDir,
        baseName + Path.GetExtension(entry.Name)
      );
    using var entryStream = entry.Open();
    using var outStream = new FileStream(
      destPath,
      FileMode.Create,
      FileAccess.Write,
      FileShare.None
    );

    var buffer = new byte[81920];
    int read;
    while ((read = await entryStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
      await outStream.WriteAsync(buffer, 0, read);
      progress?.Report(read);
    }

    archive.Dispose();
    File.Delete(zipPath);
  }

  public static long TotalCopyBytes(List<FileInfo> files) {
    var totalCopyBytes = 0L;
    foreach (var file in files) {
      totalCopyBytes += file.Length;
    }

    return totalCopyBytes;
  }

  public static long TotalExtractBytes(List<FileInfo> files) {
    var totalExtractBytes = 0L;
    foreach (var zipPath in files) {
      using var archive = ZipFile.OpenRead(zipPath.FullName);
      var entry = archive.Entries
        .FirstOrDefault(e =>
          !string.IsNullOrWhiteSpace(e.Name) &&
          !e.FullName.EndsWith("/") &&
          !e.FullName.EndsWith(@"\")
        );
      if (entry != null) totalExtractBytes += entry.Length;
    }

    return totalExtractBytes;
  }

  public static long ExtractBytes(FileInfo file) {
    var totalExtractBytes = 0L;
    using var archive = ZipFile.OpenRead(file.FullName);
    var entry = archive.Entries
      .FirstOrDefault(e =>
        !string.IsNullOrWhiteSpace(e.Name) &&
        !e.FullName.EndsWith("/") &&
        !e.FullName.EndsWith(@"\")
      );
    if (entry != null) totalExtractBytes += entry.Length;

    return totalExtractBytes;
  }
}
