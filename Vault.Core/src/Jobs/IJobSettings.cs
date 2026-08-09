namespace Vault.Core.Jobs;

public interface IJobSettings {
  string Title { get; }
  string ReadPath { get; }
  string DefaultDestination { get; }
  string WritePath => Destination ?? DefaultDestination;
  string Destination { get; }
  string Console { get; }
  string Region { get; }
  string Version { get; }
  string Name { get; }
  string Drive { get; }
}
