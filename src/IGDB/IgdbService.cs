using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Vault.IGDB;

public class IgdbService : IDisposable {
  private readonly string _clientId;
  private readonly string _clientSecret;
  private readonly HttpClient _httpClient;
  private string _accessToken;

  public IgdbService(string clientId, string clientSecret, HttpClient httpClient = null) {
    _clientId = clientId;
    _clientSecret = clientSecret;
    _httpClient = httpClient ?? new HttpClient();
  }

  public void Dispose() => _httpClient?.Dispose();

  public async Task<string> GetTokenAsync() {
    if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

    var url =
      "https://id.twitch.tv/oauth2/token" +
      "?client_id=" + _clientId +
      "&client_secret=" + _clientSecret +
      "&grant_type=client_credentials";

    using var request = new HttpRequestMessage(HttpMethod.Post, url);
    using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var token = JsonSerializer.Deserialize<IgdbTokenResponse>(json);

    _accessToken = token.AccessToken;
    return _accessToken;
  }

  public async Task<IgdbGame> SearchGameAsync(string name) {
    var token = await GetTokenAsync().ConfigureAwait(false);

    var url = "https://api.igdb.com/v4/games";
    using var request = new HttpRequestMessage(HttpMethod.Post, url);

    request.Headers.Add("Client-ID", _clientId);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = new StringContent(
      "fields name, id;\nwhere name = \"" + name.Replace("\"", "\\\"") + "\";",
      Encoding.UTF8,
      "text/plain"
    );

    using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var games = JsonSerializer.Deserialize<List<IgdbGame>>(json);

    return games != null && games.Count > 0 ? games[0] : null;
  }

  public async Task<string> SearchCoverUrlAsync(int gameId) {
    var token = await GetTokenAsync().ConfigureAwait(false);

    var url = "https://api.igdb.com/v4/covers";
    using var request = new HttpRequestMessage(HttpMethod.Post, url);

    request.Headers.Add("Client-ID", _clientId);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = new StringContent(
      "fields url;\nwhere game = " + gameId + ";",
      Encoding.UTF8,
      "text/plain"
    );

    using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var covers = JsonSerializer.Deserialize<List<IgdbCover>>(json);

    if (covers == null || covers.Count == 0 || string.IsNullOrEmpty(covers[0].Url)) return null;

    return covers[0].Url.Replace("t_thumb", "t_cover_big");
  }

  public async Task<List<string>> SearchScreenshotUrlsAsync(int gameId) {
    var token = await GetTokenAsync().ConfigureAwait(false);

    var url = "https://api.igdb.com/v4/screenshots";
    using var request = new HttpRequestMessage(HttpMethod.Post, url);

    request.Headers.Add("Client-ID", _clientId);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = new StringContent(
      "fields url;\nwhere game = " + gameId + ";",
      Encoding.UTF8,
      "text/plain"
    );

    using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var shots = JsonSerializer.Deserialize<List<IgdbScreenshot>>(json);

    if (shots == null) return new List<string>();

    return shots
      .Where(s => !string.IsNullOrEmpty(s.Url))
      .Select(s => s.Url.Replace("t_thumb", "t_cover_big"))
      .ToList();
  }
}
