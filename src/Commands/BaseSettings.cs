using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class BaseSettings : CommandSettings {
  public virtual string Title { get; set; }
  public virtual string ReadPath { get; }
  public virtual string WritePath { get; }

  [CommandOption("-c|--console")]
  [Description("Console name, e.g. ps2, switch, snes")]
  public string Console { get; set; }

  [CommandOption("-r|--region")]
  [Description("Region code, e.g. us, eu, jp")]
  public string Region { get; set; } = "USA";

  [CommandOption("-v|--version")]
  [Description("Game version, e.g. 1.0, Rev A")]
  public string Version { get; set; } = "1.0.0";

  [CommandOption("-n|--name")]
  [Description("Game name filter")]
  public string Name { get; set; }

  [CommandOption("-p|--path")]
  [Description("Drive, e.g. Z:/")]
  public string Path { get; set; } = "Z:/";
}
