namespace Vault.Core.IGDB.Data;

public union IgdbResult<T>(
  Success<T>,
  NotFound,
  Unauthorized,
  RateLimited,
  RequestFailed,
  Invalid
) {
  public static IgdbResult<T> SuccessResult(T value) => new Success<T>(value);
  public static IgdbResult<T> NotFoundResult => new NotFound();
  public static IgdbResult<T> UnauthorizedResult => new Unauthorized();
  public static IgdbResult<T> RateLimitedResult => new RateLimited();
  public static IgdbResult<T> RequestFailedResult => new RequestFailed();
  public static IgdbResult<T> InvalidResult => new Invalid();

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

  public async Task<IgdbResult<TResult>> BindAsync<TResult>(
  Func<T, Task<IgdbResult<TResult>>> bind
  ) {
    return this switch {
      Success<T> success => await bind(success.Value),
      NotFound => IgdbResult<TResult>.NotFoundResult,
      Invalid => IgdbResult<TResult>.InvalidResult,
      Unauthorized => IgdbResult<TResult>.UnauthorizedResult,
      RateLimited => IgdbResult<TResult>.RateLimitedResult,
      RequestFailed => IgdbResult<TResult>.RequestFailedResult
    };
  }
}

public record struct Success<T>(T Value);
public record struct NotFound();
public record struct Unauthorized();
public record struct RateLimited();
public record struct RequestFailed();
public record struct Invalid();

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

  public static async Task<IgdbResult<TResult>> BindAsync<T, TResult>(
    this Task<IgdbResult<T>> resultTask,
    Func<T, Task<IgdbResult<TResult>>> bind
  ) {
    var result = await resultTask;
    return await result.BindAsync(bind);
  }

  public static async Task<IgdbResult<TResult>> BindOnSuccessAsync<T, TResult>(
    this Task<IgdbResult<T>> resultTask,
    Func<T, Task<IgdbResult<TResult>>> bind
  ) {
    var result = await resultTask;

    return await result.BindAsync(bind);
  }
}
