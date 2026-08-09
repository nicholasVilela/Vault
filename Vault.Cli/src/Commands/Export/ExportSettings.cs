using System.ComponentModel;
using Spectre.Console.Cli;
using Vault.Core.Jobs;

namespace Vault.Cli.Commands;

public class ExportSettings : BaseSettings, IExportSettings {
  public override string Title => "Export";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/dump";

  [CommandOption("-e|--extract")]
  [Description("Whether files should be extracted")]
  public bool Extract { get; set; }
}
