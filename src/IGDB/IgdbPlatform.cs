using System.Text.Json.Serialization;

namespace Vault.IGDB;

public class IgdbPlatform {
  [JsonPropertyName("id")]
  public int Id { get; set; }
}
