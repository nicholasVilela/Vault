using System.ComponentModel;
using Spectre.Console.Cli;
using Vault.Core.Commands;

namespace Vault.Cli.Commands;

public class ExportSettings : BaseSettings {
  public override string Title => "Export";
  public override string ReadPath => @$"{Drive}/consoles/{Console}\roms";
  public override string DefaultDestination => @$"{Drive}/consoles/{Console}/dump";

  [CommandOption("-e|--extract")]
  [Description("Whether files should be extracted")]
  public bool Extract { get; set; }

  public override ExportOptions ToOptions() => new() {
    Title = Title,
    ReadPath = ReadPath,
    Destination = Destination,
    DefaultDestination = DefaultDestination,
    Console = Console,
    Region = Region,
    Version = Version,
    Name = Name,
    Drive = Drive,
    Extract = Extract
  };
}
