using System.ComponentModel;
using Spectre.Console.Cli;
using Vault.Core.Jobs;

namespace Vault.Cli.Commands;

public class MetadataSettings : BaseSettings, IMetadataSettings {
  public override string Title => "Metadata";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/roms";
}
