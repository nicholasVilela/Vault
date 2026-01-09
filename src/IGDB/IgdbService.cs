using System.Collections.Concurrent;
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

  private readonly ConcurrentDictionary<string, Lazy<Task<IgdbPlatform>>> _platformCache = new(StringComparer.OrdinalIgnoreCase);

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

  public Task<IgdbPlatform> SearchConsole(string name) {
    if (string.IsNullOrWhiteSpace(name)) return Task.FromResult<IgdbPlatform>(null);

    var lazy = _platformCache.GetOrAdd(
      name,
      n => new Lazy<Task<IgdbPlatform>>(() => SearchConsoleUncached(n), LazyThreadSafetyMode.ExecutionAndPublication)
    );

    return lazy.Value;
  }

  async Task<IgdbPlatform> SearchConsoleUncached(string name) {
    var token = await GetTokenAsync().ConfigureAwait(false);

    var url = "https://api.igdb.com/v4/platforms";
    using var request = new HttpRequestMessage(HttpMethod.Post, url);

    var queryName = name.Replace("\"", "\\\"").ToLowerInvariant();

    request.Headers.Add("Client-ID", _clientId);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = new StringContent(
      $"""
      fields id, name, slug;
      where slug = "{queryName}";
      limit 1;
      """,
      Encoding.UTF8,
      "text/plain"
    );

    using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var consoles = JsonSerializer.Deserialize<List<IgdbPlatform>>(json);

    if (consoles == null || consoles.Count == 0) return null;
    return consoles[0];
  }

  public async Task<IgdbGame> SearchGameAsync(string name, string consoleName) {
    var token = await GetTokenAsync().ConfigureAwait(false);

    var console = await SearchConsole(consoleName).ConfigureAwait(false);
    if (console == null) return null;

    var url = "https://api.igdb.com/v4/games";
    using var request = new HttpRequestMessage(HttpMethod.Post, url);

    var queryName = name.Replace("\"", "\\\"");

    request.Headers.Add("Client-ID", _clientId);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = new StringContent(
      $"""
      fields name, id, summary;
      search "{queryName}";
      where platforms = [{console.Id}];
      """,
      Encoding.UTF8,
      "text/plain"
    );

    using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var games = JsonSerializer.Deserialize<List<IgdbGame>>(json);

    if (games.Count == 0) return null;

    return games
      .OrderByDescending(g => string.Equals(g.Name, queryName, StringComparison.OrdinalIgnoreCase))
      .ThenByDescending(g => g.Name.StartsWith(queryName, StringComparison.OrdinalIgnoreCase))
      .ThenBy(g => g.Name.Length)
      .FirstOrDefault();
  }

  public async Task<(string coverUrl, List<string> screenshotUrls)> GetMediaAsync(int gameId, int screenshotLimit = 10) {
    var token = await GetTokenAsync().ConfigureAwait(false);

    var url = "https://api.igdb.com/v4/multiquery";
    using var request = new HttpRequestMessage(HttpMethod.Post, url);

    request.Headers.Add("Client-ID", _clientId);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    request.Content = new StringContent(
      string.Format(
        """
        query covers "cover" {{
          fields url;
          where game = {0};
          limit 1;
        }};

        query screenshots "screenshots" {{
          fields url;
          where game = {0};
          limit {1};
        }};
        """,
        gameId,
        screenshotLimit
      ),
      Encoding.UTF8,
      "text/plain"
    );

    using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    var items = JsonSerializer.Deserialize<List<IgdbMultiQueryItem>>(json);
    if (items == null || items.Count == 0) return (null, new List<string>());

    string coverUrl = null;
    var screenshots = new List<string>();

    foreach (var item in items) {
      if (item.Result.ValueKind != JsonValueKind.Array) continue;

      if (string.Equals(item.Name, "cover", StringComparison.OrdinalIgnoreCase)) {
        var covers = item.Result.Deserialize<List<IgdbCover>>();
        var raw = covers?.FirstOrDefault()?.Url;
        if (!string.IsNullOrWhiteSpace(raw)) coverUrl = raw.Replace("t_thumb", "t_cover_big");
        continue;
      }

      if (string.Equals(item.Name, "screenshots", StringComparison.OrdinalIgnoreCase)) {
        var shots = item.Result.Deserialize<List<IgdbScreenshot>>();
        if (shots != null) {
          screenshots.AddRange(
            shots
              .Where(s => !string.IsNullOrWhiteSpace(s.Url))
              .Select(s => s.Url.Replace("t_thumb", "t_cover_big"))
          );
        }
      }
    }

    return (coverUrl, screenshots);
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
