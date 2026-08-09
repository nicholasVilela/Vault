namespace Vault.Core.Jobs;

public interface IESDESettings : IJobSettings {
  string ConsoleCSV { get; }
}
