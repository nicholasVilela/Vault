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

  private readonly ConcurrentDictionary<string, Lazy<Task<IgdbResult<IgdbPlatform>>>> _platformCache = new(StringComparer.OrdinalIgnoreCase);

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
      return await ProcessTokenRequest(ct);
    }
    finally {
      _tokenLock.Release();
    }
  }

  public Task<IgdbResult<IgdbPlatform>> GetPlatform(string name) {
    if (string.IsNullOrWhiteSpace(name)) return Task.FromResult(IgdbResult<IgdbPlatform>.InvalidResult);

    return _platformCache.GetOrAdd(
      name,
      n => new Lazy<Task<IgdbResult<IgdbPlatform>>>(() => ProcessPlatformRequest(n), LazyThreadSafetyMode.ExecutionAndPublication)
    ).Value;
  }

  public async Task<IgdbResult<IgdbGame>> GetGame(string name, string platformName) {
    return await GetPlatform(platformName)
      .OnNotFoundAsync(() => Task.FromResult(ConsoleHelper.Warning($"Console not found: '{platformName}'")))
      .BindAsync(async platform => await ProcessGameRequest(name, platform));
  }

  public async Task<IgdbResult<IgdbMedia>> GetMedia(int gameId, int screenshotLimit = 10) {
    return await ProcessMediaRequest(gameId, screenshotLimit);
  }
}
