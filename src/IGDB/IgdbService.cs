using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Vault.Http;

namespace Vault.IGDB;

public class IgdbService : IDisposable{
  private readonly string _clientId;
  private readonly string _clientSecret;
  private string _accessToken;
  readonly SemaphoreSlim _tokenLock = new(1, 1);
  private HttpService _httpSvc;

  private readonly ConcurrentDictionary<string, Lazy<Task<IgdbPlatform>>> _platformCache = new(StringComparer.OrdinalIgnoreCase);

  public IgdbService(string clientId, string clientSecret) {
    _httpSvc = new HttpService(4, 1, 8);
    _clientId = clientId;
    _clientSecret = clientSecret;
  }

  public void Dispose() => _httpSvc.Dispose();

  public async Task<string> GetTokenAsync() {
    if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

    await _tokenLock.WaitAsync();
    try {
      if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

      var make = () => {
        var url = $"https://id.twitch.tv/oauth2/token?client_id={_clientId}&client_secret={_clientSecret}&grant_type=client_credentials";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        return req;
      };

      using var response = await _httpSvc.SendLimitedAsync(make);
      response.EnsureSuccessStatusCode();

      var json = await response.Content.ReadAsStringAsync();
      var token = JsonSerializer.Deserialize<IgdbTokenResponse>(json);

      _accessToken = token.AccessToken;
      return _accessToken;
    }
    finally {
      _tokenLock.Release();
    }
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
    var token = await GetTokenAsync();

    var queryName = name.Replace("\"", "\\\"").ToLowerInvariant();

    var make = () => {
      var url = "https://api.igdb.com/v4/platforms";
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _clientId);
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Content = new StringContent(
        $"""
        fields id, name, slug;
        where slug = "{queryName}";
        limit 1;
        """,
        Encoding.UTF8,
        "text/plain"
      );
      return req;
    };

    using var response = await _httpSvc.SendLimitedAsync(make);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var consoles = JsonSerializer.Deserialize<List<IgdbPlatform>>(json);

    if (consoles == null || consoles.Count == 0) return null;
    return consoles[0];
  }

  public async Task<IgdbGame> SearchGameAsync(string name, string consoleName) {
    var token = await GetTokenAsync();

    var console = await SearchConsole(consoleName);
    if (console == null) return null;

    var queryName = name.Replace("\"", "\\\"");

    var make = () => {
      var url = "https://api.igdb.com/v4/games";
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _clientId);
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Content = new StringContent(
        $"""
        fields name, id, summary;
        search "{queryName}";
        where platforms = [{console.Id}];
        """,
        Encoding.UTF8,
        "text/plain"
      );
      return req;
    };

    using var response = await _httpSvc.SendLimitedAsync(make);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var games = JsonSerializer.Deserialize<List<IgdbGame>>(json);

    if (games.Count == 0) return null;

    return games
      .OrderByDescending(g => string.Equals(g.Name, queryName, StringComparison.OrdinalIgnoreCase))
      .ThenByDescending(g => g.Name.StartsWith(queryName, StringComparison.OrdinalIgnoreCase))
      .ThenBy(g => g.Name.Length)
      .FirstOrDefault();
  }

  public async Task<(string coverUrl, List<string> screenshotUrls)> GetMediaAsync(int gameId, int screenshotLimit = 10) {
    var token = await GetTokenAsync();

    var make = () => {
      var url = "https://api.igdb.com/v4/multiquery";
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _clientId);
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Content = new StringContent(
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
      return req;
    };

    using var response = await _httpSvc.SendLimitedAsync(make);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();

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
}
