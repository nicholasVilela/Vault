using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class MetadataSettings : BaseSettings {
  public override string Title => "Metadata";
  public override string ReadPath => @$"{Path}{Console}\ROMS";
  public override string DefaultDestination => @$"{Path}{Console}\ROMS";
}
