using Vault.Core.Jobs;

namespace Vault.Api;

public abstract class RequestSettings : IJobSettings {
  public abstract string Title { get; }
  public abstract string ReadPath { get; }
  public abstract string DefaultDestination { get; }
  public string WritePath => Destination ?? DefaultDestination;
  public string Destination { get; set; }
  public string Console { get { return field != null ? field.ToLower() : ""; } set; }
  public string Region { get; set; } = "USA";
  public string Version { get; set; } = "1.0.0";
  public string Name { get; set; }
  public string Drive { get => $@"{field}:"; set; } = "Z";
}
