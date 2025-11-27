using System.Text.Json.Serialization;

namespace Vault.IGDB;

public class IgdbScreenshot {
  [JsonPropertyName("url")]
  public string Url { get; set; }
}
