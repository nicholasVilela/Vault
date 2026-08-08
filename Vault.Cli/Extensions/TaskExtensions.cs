namespace Vault.Extensions;

public static class TaskExtensions {
  public static async Task<T> Catch<T>(this Task<T> task, Action<Exception> action) {
    try {
      return await task;
    }
    catch (Exception ex) {
      action(ex);
      return default;
    }
  }

  public static async Task<T> Finally<T>(this Task<T> task, Action action) {
    try {
      return await task;
    }
    finally {
      action();
    }
  }

  public static async Task Catch(this Task task, Action<Exception> action) {
    try {
      await task;
    }
    catch (Exception ex) {
      action(ex);
    }
  }

  public static async Task Finally(this Task task, Action action) {
    try {
      await task;
    }
    finally {
      action();
    }
  }
}
