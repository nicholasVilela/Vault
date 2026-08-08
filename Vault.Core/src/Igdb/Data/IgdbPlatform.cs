using System.Text.Json.Serialization;

namespace Vault.Core.IGDB.Data;

public class IgdbPlatform {
  [JsonPropertyName("id")]
  public int Id { get; set; }
}
