using Vault.Core.Jobs;

namespace Vault.Api.Endpoints;

public class MetadataRequest : RequestSettings, IMetadataSettings {
  public override string Title => "Metadata";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/roms";
}
