using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vault.IGDB;

public sealed class RequestLimiter {
  readonly int _limit;
  readonly TimeSpan _window;

  int _count;
  long _windowStartTicks;

  readonly object _gate = new();

  public RequestLimiter(int limit, float time) {
    _limit = limit;
    _window = TimeSpan.FromSeconds(time);
    _windowStartTicks = DateTime.UtcNow.Ticks;
  }

  public ValueTask WaitAsync(CancellationToken ct = default) {
    while (true) {
      ct.ThrowIfCancellationRequested();

      TimeSpan delay;
      lock (_gate) {
        var nowTicks = DateTime.UtcNow.Ticks;
        var startTicks = _windowStartTicks;
        var elapsed = TimeSpan.FromTicks(nowTicks - startTicks);

        if (elapsed >= _window) {
          _windowStartTicks = nowTicks;
          _count = 0;
        }

        if (_count < _limit) {
          _count++;
          return ValueTask.CompletedTask;
        }

        delay = _window - elapsed;
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
      }

      return new ValueTask(Task.Delay(delay, ct));
    }
  }
}
