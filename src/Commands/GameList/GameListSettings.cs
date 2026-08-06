using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class GameListSettings : BaseSettings {
  public override string Title => "Gamelist";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}";

  [CommandOption("--no-images")]
  [Description("Do not download images.")]
  public bool NoImages { get; set; }
}
