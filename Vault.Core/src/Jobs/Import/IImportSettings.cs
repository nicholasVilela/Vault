namespace Vault.Core.Jobs;

public interface IImportSettings : IJobSettings {
  bool Move { get; }
}
