namespace Vault.Core.Jobs;

public interface IGamelistSettings : IJobSettings {
  bool NoImages { get; }
}
