using Vault.Core.Jobs;

namespace Vault.Api.Endpoints;

public class GamelistRequest : RequestSettings, IGamelistSettings {
  public override string Title => "Gamelist";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}";

  public bool NoImages { get; set; }
}
