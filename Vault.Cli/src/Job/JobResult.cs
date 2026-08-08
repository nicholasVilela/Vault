namespace Vault.Cli.Job;

public union JobResult(Success, Skip) {
  public static JobResult SuccessResult => new Success();
  public static JobResult SkipResult => new Skip();

  public async Task<JobResult> OnSuccessAsync(Func<Task> action) {
    if (this is Success) await action();
    return this;
  }

  public async Task<JobResult> OnSkipAsync(Func<Task> action) {
    if (this is Skip) await action();
    return this;
  }
}

public static class JobResultExtensions {
  public static async Task<JobResult> OnSuccessAsync(
    this Task<JobResult> resultTask,
    Func<Task> action
  ) {
    var result = await resultTask;
    return await result.OnSuccessAsync(action);
  }

  public static async Task<JobResult> OnSkipAsync(
    this Task<JobResult> resultTask,
    Func<Task> action
  ) {
    var result = await resultTask;
    return await result.OnSkipAsync(action);
  }
}

public record struct Success();
public record struct Skip();
