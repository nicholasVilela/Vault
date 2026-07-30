using System.Net.Http.Headers;

namespace Vault.IGDB;

public partial class IgdbService : IDisposable {
  public Func<HttpRequestMessage> CreatePlatformRequest(string token, string name) {
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
        """
      );
      return req;
    };

    return request;
  }

  public Func<HttpRequestMessage> CreateGameRequest(string token, int platformId, string queryName) {
    var request = () => {
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

  public Func<HttpRequestMessage> GetMediaRequest(string token, int gameId, int screenshotLimit) {
    var request = () => {
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
}
