using System.Net;
using System.Net.Http.Headers;
using Vault.IGDB;

namespace Vault.Http;

public sealed class HttpService : IDisposable {
  private readonly HttpClient _client;
  private readonly RequestLimiter _rate;
  private readonly SemaphoreSlim _concurrency;

  public HttpService(int limit, float seconds, int maxConcurrentThreads) {
    _client = new HttpClient();
    _rate = new RequestLimiter(limit, seconds);
    _concurrency = new SemaphoreSlim(maxConcurrentThreads);
  }

  public void Dispose() => _client?.Dispose();

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

          if (response.StatusCode != (HttpStatusCode)429) return response;

          if (attempt >= maxRetries) return response;

          var delay = GetRetryDelay(response.Headers, attempt);
          response.Dispose();

          await Task.Delay(delay, ct);
          continue;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) {
          if (attempt >= maxRetries) throw;
          var delay = BackoffWithJitter(attempt);
          await Task.Delay(delay, ct);
          continue;
        }
        catch (HttpRequestException) {
          if (attempt >= maxRetries) throw;
          var delay = BackoffWithJitter(attempt);
          await Task.Delay(delay, ct);
          continue;
        }
      }
      finally {
        _concurrency.Release();
      }
    }
  }

  private static TimeSpan GetRetryDelay(HttpResponseHeaders headers, int attempt) {
    if (headers.RetryAfter != null) {
      if (headers.RetryAfter.Delta.HasValue) {
        var d = headers.RetryAfter.Delta.Value;
        return d < TimeSpan.Zero ? TimeSpan.Zero : d;
      }

      if (headers.RetryAfter.Date.HasValue) {
        var d = headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
        return d < TimeSpan.Zero ? TimeSpan.Zero : d;
      }
    }

    return BackoffWithJitter(attempt);
  }

  private static TimeSpan BackoffWithJitter(int attempt) {
    var baseMs = 250 * Math.Pow(2, Math.Min(attempt, 6));
    var jitterMs = Random.Shared.Next(0, 150);
    return TimeSpan.FromMilliseconds(baseMs + jitterMs);
  }
}
