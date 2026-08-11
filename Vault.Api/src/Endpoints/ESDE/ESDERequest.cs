using Vault.Core.Jobs;

namespace Vault.Api.Endpoints;

public class ESDERequest : RequestSettings, IESDESettings  {
  public override string Title => "ES-DE";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/es-de";

  public string ConsoleCSV { get; set; }
}
