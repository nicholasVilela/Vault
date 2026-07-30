using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Vault.Helpers;
using Vault.Http;
using Vault.IGDB.Data;

namespace Vault.IGDB;

public partial class IgdbService : IDisposable{
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

    using var response = await _httpSvc.SendLimitedAsync(CreatePlatformRequest(token, name));
    response.EnsureSuccessStatusCode();

    var platforms = await response.Content.ReadFromJsonAsync<List<IgdbPlatform>>();
    if (platforms == null || platforms.Count == 0) return null;

    return platforms.First();
  }

  public async Task<IgdbResult<IgdbGame>> GetGame(string name, string platformName) {
    var token = await GetToken();

    var platform = await GetPlatform(platformName);
    if (platform == null) return IgdbResult<IgdbGame>.NotFound;

    var queryName = name.Replace("\"", "\\\"");
    using var response = await _httpSvc.SendLimitedAsync(CreateGameRequest(token, platform.Id, queryName));
    response.EnsureSuccessStatusCode();

    var games = await response.Content.ReadFromJsonAsync<List<IgdbGame>>();
    if (games.Count == 0) return IgdbResult<IgdbGame>.NotFound;

    var game = games
      .OrderByDescending(g => string.Equals(g.Name, queryName, StringComparison.OrdinalIgnoreCase))
      .ThenByDescending(g => g.Name.StartsWith(queryName, StringComparison.OrdinalIgnoreCase))
      .ThenBy(g => g.Name.Length)
      .FirstOrDefault();

    return IgdbResult<IgdbGame>.Success(game);
  }

  public async Task<IgdbResult<IgdbMedia>> GetMedia(int gameId, int screenshotLimit = 10) {
    var token = await GetToken();
    
    using var response = await _httpSvc.SendLimitedAsync(GetMediaRequest(token, gameId, screenshotLimit));
    response.EnsureSuccessStatusCode();
    
    var items = await response.Content.ReadFromJsonAsync<List<IgdbMultiQueryItem>>();
    if (items == null || items.Count == 0) return IgdbResult<IgdbMedia>.NotFound;

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

    return IgdbResult<IgdbMedia>.Success(new IgdbMedia(coverUrl, screenshots));
  }
}
