using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vault.Http;
using Vault.IGDB.Data;

namespace Vault.IGDB;

public class IgdbService : IDisposable{
  private readonly IgdbOptions _options;
  private string _accessToken;
  private DateTimeOffset _accessTokenExpiration;
  readonly SemaphoreSlim _tokenLock = new(1, 1);
  private HttpService _httpSvc;

  private readonly ConcurrentDictionary<string, Lazy<Task<IgdbPlatform>>> _platformCache = new(StringComparer.OrdinalIgnoreCase);

  public IgdbService(IOptions<IgdbOptions> options) {
    _httpSvc = new HttpService(4, 1, 8);
    _options = options.Value;
  }

  public void Dispose() => _httpSvc.Dispose();
  private bool HasValidToken() => !string.IsNullOrWhiteSpace(_accessToken) && DateTimeOffset.UtcNow < _accessTokenExpiration;

  public async Task<string> GetToken(CancellationToken ct = default) {
    if (HasValidToken()) return _accessToken;

    await _tokenLock.WaitAsync(ct);

    try {
      if (HasValidToken()) return _accessToken;

      var request = () => {
        var url = IgdbRoutes.Token(_options.ClientId, _options.ClientSecret);
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        return req;
      };

      using var response = await _httpSvc.SendLimitedAsync(request, ct: ct);
      response.EnsureSuccessStatusCode();

      var token = await response.Content.ReadFromJsonAsync<IgdbTokenResponse>(ct);
      _accessToken = token.AccessToken;
      _accessTokenExpiration = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn).AddMinutes(-1);
      
      return _accessToken;
    }
    finally {
      _tokenLock.Release();
    }
  }

  public Task<IgdbPlatform> GetPlatform(string name) {
    if (string.IsNullOrWhiteSpace(name)) return null;

    return _platformCache.GetOrAdd(
      name,
      n => new Lazy<Task<IgdbPlatform>>(() => GetPlatformUncached(n), LazyThreadSafetyMode.ExecutionAndPublication)
    ).Value;
  }

  private async Task<IgdbPlatform> GetPlatformUncached(string name) {
    var token = await GetToken();

    var queryName = name.Replace("\"", "\\\"").ToLowerInvariant();

    var request = () => {
      var url = IgdbRoutes.Platforms;
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _options.ClientId);
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

    using var response = await _httpSvc.SendLimitedAsync(request);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var consoles = JsonSerializer.Deserialize<List<IgdbPlatform>>(json);

    if (consoles == null || consoles.Count == 0) return null;
    return consoles[0];
  }

  public async Task<IgdbGame> GetGame(string name, string platformName) {
    var token = await GetToken();

    var platform = await GetPlatform(platformName);
    if (platform == null) return null;

    var queryName = name.Replace("\"", "\\\"");

    var request = () => {
      var url = IgdbRoutes.Games;

      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _options.ClientId);
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Content = new StringContent(
        $"""
        fields name, id, summary;
        search "{queryName}";
        where platforms = [{platform.Id}];
        """,
        Encoding.UTF8,
        "text/plain"
      );
      
      return req;
    };

    using var response = await _httpSvc.SendLimitedAsync(request);
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

  public async Task<(string coverUrl, List<string> screenshotUrls)> GetMedia(int gameId, int screenshotLimit = 10) {
    var token = await GetToken();

    var request = () => {
      var url = IgdbRoutes.Multiquery;
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _options.ClientId);
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

    using var response = await _httpSvc.SendLimitedAsync(request);
    response.EnsureSuccessStatusCode();

    var items = await response.Content.ReadFromJsonAsync<List<IgdbMultiQueryItem>>();
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
