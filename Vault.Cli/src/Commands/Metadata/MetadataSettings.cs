using Vault.Core.Jobs;

namespace Vault.Cli.Commands;

public class MetadataSettings : BaseSettings {
  public override string Title => "Metadata";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/roms";

  public override MetadataOptions ToOptions() => new() {
    Title = Title,
    ReadPath = ReadPath,
    Destination = Destination,
    DefaultDestination = DefaultDestination,
    Console = Console,
    Region = Region,
    Version = Version,
    Name = Name,
    Drive = Drive
  };
}
