using System.ComponentModel;
using Spectre.Console.Cli;
using Vault.Core.Jobs;

namespace Vault.Cli.Commands;

public class GamelistSettings : BaseSettings, IGamelistSettings {
  public override string Title => "Gamelist";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}";

  [CommandOption("--no-images")]
  [Description("Do not download images.")]
  public bool NoImages { get; set; }
}
