using System.ComponentModel;
using Spectre.Console.Cli;
using Vault.Core.Commands;

namespace Vault.Cli.Commands;

public class GamelistSettings : BaseSettings {
  public override string Title => "Gamelist";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}";

  [CommandOption("--no-images")]
  [Description("Do not download images.")]
  public bool NoImages { get; set; }

  public override GamelistOptions ToOptions() => new() {
    Title = Title,
    ReadPath = ReadPath,
    Destination = Destination,
    DefaultDestination = DefaultDestination,
    Console = Console,
    Region = Region,
    Version = Version,
    Name = Name,
    Drive = Drive,
    NoImages = NoImages
  };
}
