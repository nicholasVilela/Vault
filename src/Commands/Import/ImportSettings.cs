using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class ImportSettings : BaseSettings {
  public override string Title => "Imported";
  public override string ReadPath => @$"{Path}CONSOLES\{Console}\IMPORT";
  public override string DefaultDestination => @$"{Path}CONSOLES\{Console}\ROMS";

  [CommandOption("-m|--move")]
  [Description("Whether files should be moved or copied to destination.")]
  public bool Move { get; set; }

}
