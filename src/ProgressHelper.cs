using Spectre.Console;

namespace Vault;

public static class ProgressHelper {
  public static async Task Build(ProgressTask task, long total, Func<IProgress<long>, Task> operation) {
    var lastReported = 0L;

    var progress = new Progress<long>(bytes => {
      if (bytes <= lastReported) return;

      var delta = bytes - lastReported;
      lastReported = bytes;
      task.Increment(delta);
    });

    await operation(progress);

    if (lastReported < total) task.Increment(total - lastReported);
  }
}
