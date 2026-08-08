using System.Text.Json.Serialization;

namespace Vault.Core.IGDB.Data;

public class IgdbCover {
  [JsonPropertyName("url")]
  public string Url { get; set; }
}
