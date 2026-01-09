namespace Vault.Data;

public class Metadata {
  public string Title { get; set; }
  public int GameId { get; set; }
  public string GameCode { get; set; }
  public string Platform { get; set; }
  public string Summary { get; set; }
  public MediaBlock Media { get; set; }

  public class MediaBlock {
    public string Cover { get; set; }
    public List<string> Screenshots { get; set; }
  }
}
