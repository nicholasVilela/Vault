namespace Vault;

public static class TaskExtensions {
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
