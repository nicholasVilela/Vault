using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class InfoSettings : BaseSettings {
  public override string Title => "Info";
  public override string ReadPath => @$"{Path}{Console}\ROMS";
  public override string DefaultDestination => @$"{Path}{Console}";
}
