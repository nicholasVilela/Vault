using System.ComponentModel;
using Spectre.Console.Cli;

namespace Vault.Commands;

public class ImportSettings : BaseSettings {
  public override string Title => "Imported";
  public override string ReadPath => $"{Path}{Console}/Games";
  public override string WritePath => $"{Path}{Console}";
}
