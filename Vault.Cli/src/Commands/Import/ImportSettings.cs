using System.ComponentModel;
using Spectre.Console.Cli;
using Vault.Core.Jobs;

namespace Vault.Cli.Commands;

public class ImportSettings : BaseSettings, IImportSettings {
  public override string Title => "Import";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/import";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/roms";

  [CommandOption("-m|--move")]
  [Description("Whether files should be moved or copied to destination.")]
  public bool Move { get; set; }
}
