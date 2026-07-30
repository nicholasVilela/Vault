namespace Vault.IGDB.Data;

public union IgdbResult<T>(
  Success<T>,
  NotFound,
  Unauthorized,
  RateLimited,
  RequestFailed
) {
  public static IgdbResult<T> Success(T value) => new Success<T>(value);
  public static IgdbResult<T> NotFound => new NotFound();
  public static IgdbResult<T> Unauthorized => new Unauthorized();
  public static IgdbResult<T> RateLimited => new RateLimited();
  public static IgdbResult<T> RequestFailed => new RequestFailed();

  public IgdbResult<T> OnSuccess(Action<T> action) {
    if (this is Success<T> success) action(success.Value);
    return this;
  }

  public async Task<IgdbResult<T>> OnSuccessAsync(Func<T, Task> action) {
    if (this is Success<T> success) await action(success.Value);
    return this;
  }

  public IgdbResult<T> OnNotFound(Action action) {
    if (this is NotFound) action();
    return this;
  }

  public async Task<IgdbResult<T>> OnNotFoundAsync(Func<Task> action) {
    if (this is NotFound) await action();
    return this;
  }

  public IgdbResult<T> OnUnauthorized(Action action) {
    if (this is Unauthorized) action();
    return this;
  }

  public IgdbResult<T> OnRateLimited(Action action) {
    if (this is RateLimited) action();
    return this;
  }
}

public record struct Success<T>(T Value);
public record struct NotFound();
public record struct Unauthorized();
public record struct RateLimited();
public record struct RequestFailed();

public static class IgdbResultExtensions {
  public static async Task<IgdbResult<T>> OnSuccessAsync<T>(
    this Task<IgdbResult<T>> resultTask,
    Func<T, Task> action
  ) {
    var result = await resultTask;
    return await result.OnSuccessAsync(action);
  }

  public static async Task<IgdbResult<T>> OnNotFoundAsync<T>(
    this Task<IgdbResult<T>> resultTask,
    Func<Task> action
  ) {
    var result = await resultTask;
    return await result.OnNotFoundAsync(action);
  }

  public static IgdbResult<T> OnNotFound<T>(
    this Task<IgdbResult<T>> resultTask,
    Action action
  ) {
    var result = resultTask;
    return result.OnNotFound(action);
  }
}
