using System.Collections.Concurrent;

namespace Vault.Core.Message;

public class MessageService : IDisposable {
  public ConcurrentBag<string> Warnings { get; set; } = new();
  public ConcurrentBag<string> Errors { get; set; } = new();

  public int Warning(string message) {
    Warnings.Add(message);
    return 0;
  }

  public int Error(string message) {
    Errors.Add(message);
    return 0;
  }

  public void Dispose() {}
}
