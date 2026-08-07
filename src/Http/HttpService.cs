using System.Net;

namespace Vault.Http;

public sealed class HttpService : IDisposable {
  private readonly HttpClient _client;
  private readonly RequestLimiter _rate;
  private readonly SemaphoreSlim _concurrency;

  private const int RetryDelayMs = 2000;


  public HttpService(int limit, float seconds, int maxConcurrentThreads) {
    _client = new HttpClient();
    _rate = new RequestLimiter(limit, seconds);
    _concurrency = new SemaphoreSlim(maxConcurrentThreads);
  }

  public void Dispose() => _client?.Dispose();

  public async Task<HttpResponseMessage> SendLimitedAsync(
    Func<Task<HttpRequestMessage>> createRequest,
    int maxRetries = 5,
    CancellationToken ct = default
  ) {
    for (var attempt = 0; ; attempt++) {
      ct.ThrowIfCancellationRequested();

      using var request = createRequest();

      await _concurrency.WaitAsync(ct);
      try {
        await _rate.WaitAsync(ct);

        HttpResponseMessage response = null;
        try {
          response = await _client.SendAsync(await request, ct);
          response.EnsureSuccessStatusCode();

          if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;
          if (attempt >= maxRetries) return response;

          response.Dispose();

          await Task.Delay(RetryDelayMs, ct);
          continue;
        }
        catch (Exception ex) {
          Console.WriteLine(ex.GetType().FullName);
          Console.WriteLine(ex.Message);
          throw;
        }
      }
      finally {
        _concurrency.Release();
      }
    }
  }


  public async Task<HttpResponseMessage> SendLimitedAsync(
    Func<HttpRequestMessage> createRequest,
    int maxRetries = 5,
    CancellationToken ct = default
  ) {
    for (var attempt = 0; ; attempt++) {
      ct.ThrowIfCancellationRequested();

      using var request = createRequest();

      await _concurrency.WaitAsync(ct);
      try {
        await _rate.WaitAsync(ct);

        HttpResponseMessage response = null;
        try {
          response = await _client.SendAsync(request, ct);

          if (response.StatusCode != HttpStatusCode.TooManyRequests) return response;
          if (attempt >= maxRetries) return response;

          response.Dispose();

          await Task.Delay(RetryDelayMs, ct);
          continue;
        }
        catch (Exception ex) {
          Console.WriteLine(ex.GetType().FullName);
          Console.WriteLine(ex.Message);
          throw;
        }
      }
      finally {
        _concurrency.Release();
      }
    }
  }

  public async Task<HttpResponseMessage> GetAsync(string url) {
    return await _client.GetAsync(url);
  }
}
