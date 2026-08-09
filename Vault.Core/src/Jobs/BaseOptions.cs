namespace Vault.Core.Jobs;

public class BaseOptions {
  public string Title { get; set; }
  public string ReadPath { get; set; }
  public string DefaultDestination { get; set; }
  public string WritePath => Destination ?? DefaultDestination;
  public string Destination { get; set; }
  public string Console { get { return field != null ? field.ToLower() : ""; } set; }
  public string Region { get; set; } = "USA";
  public string Version { get; set; } = "1.0.0";
  public string Name { get; set; }
  public string Drive { get => $"{field}:"; set; } = "Z";
}
