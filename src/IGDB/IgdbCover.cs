using System.Text.Json.Serialization;

namespace Vault.IGDB;

public class IgdbCover {
  [JsonPropertyName("url")]
  public string Url { get; set; }
}
