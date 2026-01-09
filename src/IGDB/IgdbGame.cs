using System.Text.Json.Serialization;

namespace Vault.IGDB;

public class IgdbGame {
  [JsonPropertyName("id")]
  public int Id { get; set; }
  
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("summary")]
  public string Summary { get; set; }
}
