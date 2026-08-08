using System.Text.Json.Serialization;

namespace Vault.Core.IGDB.Data;

public class IgdbScreenshot {
  [JsonPropertyName("url")]
  public string Url { get; set; }
}
