using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vault.Core.IGDB.Data;

public class IgdbMultiQueryItem {
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [JsonPropertyName("result")]
  public JsonElement Result { get; set; }
}
