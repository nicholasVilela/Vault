using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class GameListSettings : BaseSettings {
  public override string Title => "Info";
  public override string ReadPath => @$"{Path}{Console}\ROMS";
  public override string DefaultDestination => @$"{Path}{Console}";

  [CommandOption("--no-images")]
  [Description("Do not download images.")]
  public bool NoImages { get; set; }
}
