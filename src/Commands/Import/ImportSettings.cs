using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class ImportSettings : BaseSettings {
  public override string Title => "Imported";
  public override string ReadPath => @$"{Path}{Console}\IMPORT";
  public override string DefaultDestination => @$"{Path}{Console}\ROMS";
<<<<<<< HEAD

  [CommandOption("-m|--move")]
  [Description("Whether files should be moved or copied to destination.")]
  public bool Move { get; set; }
=======
>>>>>>> 1cbe9fe2ce03e5f7a71f6d394b0e57a75c53b4f5
}
