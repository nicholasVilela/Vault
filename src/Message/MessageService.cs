using System.Collections.Concurrent;
using Spectre.Console;
using Vault.Helpers;

namespace Vault.Message;

public class MessageService : IDisposable {
  public ConcurrentBag<string> Warnings { get; set; } = new();
  public ConcurrentBag<string> Errors { get; set; } = new();

  public MessageService() {
    
  }

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
