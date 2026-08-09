namespace Vault.Core.Jobs;

public interface IExportSettings : IJobSettings {
  bool Extract { get; }
}
