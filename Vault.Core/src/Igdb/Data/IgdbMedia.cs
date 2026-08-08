namespace Vault.Core.IGDB.Data;

public record class IgdbMedia(string Cover, List<string> Screenshots) {
  public static IgdbMedia Empty => new IgdbMedia(null, null);
}
