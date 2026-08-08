namespace Vault.Cli.Commands;

public class MetadataSettings : BaseSettings {
  public override string Title => "Metadata";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/roms";
}
