using System.Net.Http.Headers;
using System.Text.Json;
using Vault.Core.IGDB.Data;

namespace Vault.Core.IGDB;

public partial class IgdbService : IDisposable {
  public Func<HttpRequestMessage> CreateTokenRequest() {
    var request = () => {
      var url = IgdbRoutes.Token(_options.ClientId, _options.ClientSecret);
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      return req;
    };

    return request;
  }

  public Func<Task<HttpRequestMessage>> CreatePlatformRequest(string name) {
    var queryName = name.Replace("\"", "\\\"").ToLowerInvariant();
    var request = async () => {
      var token = await GetToken();

      var url = IgdbRoutes.Platforms;
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _options.ClientId);
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Content = new StringContent(
        $"""
        fields id, name, slug;
        where slug = "{queryName}";
        limit 1;
        """
      );
      return req;
    };

    return request;
  }

  public Func<Task<HttpRequestMessage>> CreateGameRequest(int platformId, string queryName) {
    var request = async () => {
      var token = await GetToken();
      var url = IgdbRoutes.Games;
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _options.ClientId);
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Content = new StringContent(
        $"""
        fields name, id, summary;
        search "{queryName}";
        where platforms = [{platformId}];
        """
      );
      
      return req;
    };

    return request;
  }

  public Func<Task<HttpRequestMessage>> GetMediaRequest(int gameId, int screenshotLimit) {
    var request = async () => {
      var token = await GetToken();

      var url = IgdbRoutes.Multiquery;
      var req = new HttpRequestMessage(HttpMethod.Post, url);
      req.Headers.Add("Client-ID", _options.ClientId);
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      req.Content = new StringContent(
        $$"""
        query covers "cover" {
          fields url;
          where game = {{gameId}};
          limit 1;
        };

        query screenshots "screenshots" {
          fields url;
          where game = {{gameId}};
          limit {{screenshotLimit}};
        };
        """
      );
      return req;
    };

    return request;
  }
  
  private async Task<string> ProcessTokenRequest(CancellationToken ct) {
    if (HasValidToken()) return _accessToken;
    
    using var response = await _httpSvc.SendLimitedAsync(CreateTokenRequest(), ct: ct);

    var token = await response.Content.ReadFromJsonAsync<IgdbTokenResponse>(ct);
    _accessToken = token.AccessToken;
    _accessTokenExpiration = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn).AddMinutes(-1);
    
    return _accessToken;
  }

  private async Task<IgdbResult<IgdbPlatform>> ProcessPlatformRequest(string name) {
    using var response = await _httpSvc.SendLimitedAsync(CreatePlatformRequest(name));

    var platforms = await response.Content.ReadFromJsonAsync<List<IgdbPlatform>>();
    if (platforms == null || platforms.Count == 0) return IgdbResult<IgdbPlatform>.NotFoundResult;

    return IgdbResult<IgdbPlatform>.SuccessResult(platforms.First());
  }

  private async Task<IgdbResult<IgdbGame>> ProcessGameRequest(string name, IgdbPlatform platform) {
    var queryName = name.Replace("\"", "\\\"");
    using var response = await _httpSvc.SendLimitedAsync(CreateGameRequest(platform.Id, queryName));

    var games = await response.Content.ReadFromJsonAsync<List<IgdbGame>>();
    if (games == null || games.Count == 0) return IgdbResult<IgdbGame>.NotFoundResult;

    var game = games
      .OrderByDescending(g => string.Equals(g.Name, queryName, StringComparison.OrdinalIgnoreCase))
      .ThenByDescending(g => g.Name.StartsWith(queryName, StringComparison.OrdinalIgnoreCase))
      .ThenBy(g => g.Name.Length)
      .FirstOrDefault();

    return IgdbResult<IgdbGame>.SuccessResult(game);
  }

  private async Task<IgdbResult<IgdbMedia>> ProcessMediaRequest(int gameId, int screenshotLimit = 10) {
    using var response = await _httpSvc.SendLimitedAsync(GetMediaRequest(gameId, screenshotLimit));
    
    var items = await response.Content.ReadFromJsonAsync<List<IgdbMultiQueryItem>>();
    if (items == null || items.Count == 0) return IgdbResult<IgdbMedia>.NotFoundResult;

    string coverUrl = null;
    var screenshots = new List<string>();

    foreach (var item in items) {
      if (item.Result.ValueKind != JsonValueKind.Array) continue;

      if (item.Name == "cover") {
        var covers = item.Result.Deserialize<List<IgdbCover>>();
        if (covers == null || covers.Count == 0) continue;
        var raw = covers.FirstOrDefault().Url;
        if (!string.IsNullOrWhiteSpace(raw)) coverUrl = raw.Replace("t_thumb", "t_cover_big");
        continue;
      }

      if (item.Name == "screenshots") {
        var shots = item.Result.Deserialize<List<IgdbScreenshot>>();
        if (shots == null) continue;

        screenshots.AddRange(
          shots
            .Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .Select(s => s.Url.Replace("t_thumb", "t_cover_big"))
        );
      }
    }

    return IgdbResult<IgdbMedia>.SuccessResult(new IgdbMedia(coverUrl, screenshots));
  }
}
