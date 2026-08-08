using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class ExportSettings : BaseSettings {
  public override string Title => "Export";
  public override string ReadPath => @$"{Drive}/consoles/{Console}\roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/dump";

  [CommandOption("-e|--extract")]
  [Description("Whether files should be extracted")]
  public bool Extract { get; set; }
}
