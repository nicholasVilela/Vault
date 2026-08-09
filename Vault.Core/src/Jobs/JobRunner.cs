using Vault.Core.Extensions;
using Vault.Core.Message;

namespace Vault.Core.Jobs;

public class JobRunner<TOption> where TOption : IJobSettings {
  private Func<FileInfo, (string name, string displayName)> _onGetNames { get; set; }
  private Func<FileInfo, string, string, Action<long>, Task<JobResult>> _onProcess { get; set; }
  private Func<TOption, List<FileInfo>> _onGetFiles { get; set; }
  private Func<List<FileInfo>, long> _onGetWork { get; set; }
  private Action _onFinalize { get; set; }

  private Dictionary<Func<bool>, Action> _assertions { get; set; } = new();

  private JobRunnerOptions _dispatcherOptions { get; set; }
  private TOption _options { get; set; }

  private int _fileCount;
  private int _processed;
  private int _skipped;
  private long _totalWork;
  private long _completedWork;

  private List<FileInfo> _files { get; set; }

  public Action OnFinalize => _onFinalize;
  public Func<List<FileInfo>, long> OnGetWork => _onGetWork;
  public Func<TOption, List<FileInfo>> OnGetFiles => _onGetFiles;

  public TOption Options => _options;
  public JobProgress Progress => new (
    Volatile.Read(ref _processed),
    Volatile.Read(ref _skipped),
    Volatile.Read(ref _fileCount),
    Volatile.Read(ref _completedWork),
    Volatile.Read(ref _totalWork)
  );

  public JobRunner<TOption> GetNames(Func<FileInfo, (string name, string displayName)> func) {
    _onGetNames += func;
    return this;
  }

  public JobRunner<TOption> GetProcess(Func<FileInfo, string, string, Action<long>, Task<JobResult>> func) {
    _onProcess += func;
    return this;
  }
  
  public JobRunner<TOption> GetFiles(Func<TOption, List<FileInfo>> func) {
    _onGetFiles += func;
    return this;
  }

  public JobRunner<TOption> GetWork(Func<List<FileInfo>, long> func) {
    _onGetWork += func;
    return this;
  }

  public JobRunner<TOption> Finalize(Action func) {
    _onFinalize += func;
    return this;
  }

  public JobRunner<TOption> Assert(Func<bool> func, Action action) {
    _assertions.Add(func, action);
    return this;
  }

  public JobRunner<TOption> WithDispatcherOptions(JobRunnerOptions options) {
    _dispatcherOptions = options;
    return this;
  }

  public JobRunner<TOption> WithJobOptions(TOption options) {
    _options = options;
    return this;
  }

  public void Initialize(MessageService messageSvc) {
    if (!CheckAssertions()) return;

    _files = _onGetFiles(_options);
    if (_files.Count == 0) {
      messageSvc.Error($"No game files found in: '{_options.ReadPath}'{(!string.IsNullOrEmpty(_options.Name) ? $" with name: '{_options.Name}'" : "")}");
      return;
    }

    Volatile.Write(ref _fileCount, _files.Count);
    Volatile.Write(ref _totalWork, _onGetWork(_files));
  }

  public async Task Run(MessageService messageSvc) {
    using var semaphore = new SemaphoreSlim(_dispatcherOptions.MaxThreads);

    var tasks = new List<Task>();
    foreach (var file in _files) {
      var (name, displayName) = _onGetNames(file);
      tasks.Add(Task.Run(async () => {
        await semaphore.WaitAsync();
        await _onProcess(file, name, displayName, amount => Interlocked.Add(ref _completedWork, amount))
          .OnSkipAsync(async () => {
            Interlocked.Increment(ref _skipped);
          })
          .Catch(ex => messageSvc.Error($"Error processing {displayName}: {ex.Message}"))
          .Finally(() => {
            semaphore.Release();
            Interlocked.Increment(ref _processed);
          });
      }));
    }

    await Task.WhenAll(tasks);
  }

  private bool CheckAssertions() {
    foreach (var (assertion, action) in _assertions) {
      if (!assertion()) continue;

      action();
      return false;;
    }

    return true;
  }
}
