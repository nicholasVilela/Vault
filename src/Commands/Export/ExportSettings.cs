using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class ExportSettings : BaseSettings {
  public override string Title => "Exported";
  public override string ReadPath => @$"{Path}{Console}\ROMS";
  public override string DefaultDestination => @$"{Path}{Console}\DUMP";

  [CommandOption("-e|--extract")]
  [Description("Whether files should be extracted")]
  public bool Extract { get; set; }
}
