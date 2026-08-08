using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Cli.Commands;

public class ESDESettings : BaseSettings {
  public override string Title => "ES-DE";
  public override string ReadPath => @$"{Drive}/consoles/{Console}/roms";
  public override string DefaultDestination => @$"{Drive}/es-de";

  [CommandOption("-l|--list")]
  [Description("Consoles to include, format as CSV, e.g. 'wiiu,3ds,gba'")]
  public string ConsoleCSV { get; set; }
}
