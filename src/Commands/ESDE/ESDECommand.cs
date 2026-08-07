using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Xml;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using Vault.Message;
using Vault.Helpers;
using Vault.IGDB;
using Vault.IGDB.Data;

namespace Vault.Commands;

public class ESDECommand : AsyncCommand<ESDESettings> {
  private readonly MessageService _messageSvc;
  const int OverheadUnitsPerGame = 2;

  public ESDECommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, ESDESettings settings, CancellationToken _cancellationToken) {
    var files = GetFiles(settings).Where(file => Path.Exists($"{file.FullName}/gamelist.xml")).ToList();
    if (files.Count == 0) _messageSvc.Error($"No game files found in: '{settings.ConsoleCSV}'");

    Directory.CreateDirectory($"{settings.WritePath}/gamelists");
    Directory.CreateDirectory($"{settings.WritePath}/downloaded_media/covers");

    await ConsoleHelper.Build(
      files,
      settings,
      totalWork: files.Count * OverheadUnitsPerGame,
      maxConcurrency: 100,
      processFile: (file, fileName, displayName, task) => Process(fileName, displayName, settings, task),
      getNames: file => (file.FullName, GetConsoleName(file.Name.ToLower())),
      displayPlatform: false,
      suffix: "Console",
      messageSvc: _messageSvc
    );

    return 0;
  }

  public async Task Process(
    string folderPath,
    string console,
    ESDESettings settings,
    ProgressTask progress
  ) {
    var sourceGamelistPath = $"{folderPath}/gamelist.xml";
    var targetGamelistPath = $"{settings.WritePath}/gamelists/{console}";
    Directory.CreateDirectory(targetGamelistPath);
    await FileHelper.Copy(sourceGamelistPath, $"{targetGamelistPath}/gamelist.xml");
    progress.Increment(1);

    var sourceImagesPath = $"{folderPath}/images";
    var targetImagesPath = $"{settings.WritePath}/downloaded_media/covers/{console}";
    Directory.CreateDirectory(targetImagesPath);
    foreach (var imagePath in Directory.EnumerateFiles(sourceImagesPath)) await FileHelper.Copy(imagePath, $"{targetImagesPath}/{new FileInfo(imagePath).Name}");
    progress.Increment(1);
  }

  public List<FileInfo> GetFiles(ESDESettings settings) {
    if (string.IsNullOrEmpty(settings.ConsoleCSV)) {
      return Directory.EnumerateDirectories($"{settings.Drive}/consoles").Select(file => new FileInfo(file)).ToList();
    }

    var files = new List<FileInfo>();
    var consoles = settings.ConsoleCSV.Split(",");
    foreach (var console in consoles) {
      var directory = $"{settings.Drive}/consoles/{console.ToUpper()}";
      files.Add(new FileInfo(directory));
    }

    return files;
  }

  public string GetConsoleName(string name) {
    return name switch {
      "3ds" => "n3ds",
      _ => name
    };
  }
}
