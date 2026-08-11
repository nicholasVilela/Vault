using System.ComponentModel;
using Vault.Core.Jobs;

namespace Vault.Api.Endpoints;

public class ImportRequest : RequestSettings, IImportSettings {
  public override string Title => "Import";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/import";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/roms";

  public bool Move { get; set; }
}
