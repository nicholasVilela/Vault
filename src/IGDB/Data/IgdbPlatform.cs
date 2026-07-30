using System.Text.Json.Serialization;

namespace Vault.IGDB.Data;

public class IgdbPlatform {
  [JsonPropertyName("id")]
  public int Id { get; set; }
}
