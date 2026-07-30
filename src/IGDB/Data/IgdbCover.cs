using System.Text.Json.Serialization;

namespace Vault.IGDB.Data;

public class IgdbCover {
  [JsonPropertyName("url")]
  public string Url { get; set; }
}
