using System.IO.Compression;
using Spectre.Console;

namespace Vault;

public static class FileHelper {
  public static async Task Copy(
    string sourcePath,
    string destPath
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
    }
  }

  public static void Extract(string zipPath) {
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
    while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0) {
      outStream.Write(buffer, 0, read);
    }

    archive.Dispose();
    File.Delete(zipPath);
  }
}
