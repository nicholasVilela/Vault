using System.IO.Compression;
using Spectre.Console;

namespace Vault.Helpers;

public static class FileHelper {
  const int BufferSize = 81920;

  public static async Task Copy(
    string sourcePath,
    string destPath,
    IProgress<long> progress = null
  ) {
    var buffer = new byte[BufferSize];

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
    var entry = GetZipEntry(archive, zipPath);

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

    var buffer = new byte[BufferSize];
    int read;
    while ((read = await entryStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
      await outStream.WriteAsync(buffer, 0, read);
      progress?.Report(read);
    }

    archive.Dispose();
    File.Delete(zipPath);
  }

  public static long TotalCopyBytes(List<FileInfo> files) => files.Sum(f => f.Length);
  public static long TotalExtractBytes(List<FileInfo> files) => files.Sum(ExtractBytes);

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

  private static ZipArchiveEntry GetZipEntry(ZipArchive archive, string zipPath) {
    var entry = archive.Entries
      .FirstOrDefault(e =>
        !string.IsNullOrWhiteSpace(e.Name) &&
        !e.FullName.EndsWith("/") &&
        !e.FullName.EndsWith(@"\")
      );

    if (entry == null) throw new InvalidOperationException($"ZIP '{zipPath}' contains no files.");

    return entry;
  }

  public static void Move(
    string sourcePath,
    string destPath,
    IProgress<long> progress = null
  ) {
    var sourceInfo = new FileInfo(sourcePath);
    var totalBytes = sourceInfo.Length;

    File.Move(sourcePath, destPath);
    progress?.Report(totalBytes);
  }
}
