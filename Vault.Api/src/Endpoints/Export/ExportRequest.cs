using System.ComponentModel;
using Vault.Api;
using Vault.Core.Jobs;

namespace Vault.Api.Endpoints;

public class ExportRequest : RequestSettings, IExportSettings {
  public override string Title => "Export";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/dump";

  public bool Extract { get; set; }
}
