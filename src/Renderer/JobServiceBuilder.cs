using Spectre.Console;
using Vault.Commands;
using Vault.Message;

namespace Vault.Renderer;

public class JobServiceBuilder<TSettings> where TSettings : BaseSettings {
  private Func<FileInfo, (string name, string displayName)> _onGetNames { get; set; }
  private Func<FileInfo, string, string, ProgressTask, Task> _onProcess { get; set; }
  private Func<TSettings, List<FileInfo>> _onGetFiles { get; set; }
  private Func<List<FileInfo>, long> _onGetWork { get; set; }
  private Func<bool> _onValidate { get; set; }
  private Action _onFinalize { get; set; }

  private JobOptions _jobOptions { get; set; }
  private RenderOptions _renderOptions { get; set; }
  private TSettings _settings { get; set; }

  public JobServiceBuilder<TSettings> GetNames(Func<FileInfo, (string name, string displayName)> func) {
    _onGetNames += func;
    return this;
  }

  public JobServiceBuilder<TSettings> GetProcess(Func<FileInfo, string, string, ProgressTask, Task> func) {
    _onProcess += func;
    return this;
  }
  
  public JobServiceBuilder<TSettings> GetFiles(Func<TSettings, List<FileInfo>> func) {
    _onGetFiles += func;
    return this;
  }

  public JobServiceBuilder<TSettings> GetWork(Func<List<FileInfo>, long> func) {
    _onGetWork += func;
    return this;
  }

  public JobServiceBuilder<TSettings> Validate(Func<bool> func) {
    _onValidate += func;
    return this;
  }

  public JobServiceBuilder<TSettings> Finalize(Action func) {
    _onFinalize += func;
    return this;
  }

  public JobServiceBuilder<TSettings> WithJobOptions(JobOptions options) {
    _jobOptions = options;
    return this;
  }

  public JobServiceBuilder<TSettings> WithRenderOptions(RenderOptions options) {
    _renderOptions = options;
    return this;
  }
  
  public JobServiceBuilder<TSettings> WithSettings(TSettings settings) {
    _settings = settings;
    return this;
  }

  public async Task Run(MessageService messageSvc) {
    var job = new JobService<TSettings>(messageSvc);

    if (_onGetNames != null) job.OnGetNames += _onGetNames;
    if (_onProcess  != null) job.OnProcess  += _onProcess;
    if (_onGetFiles != null) job.OnGetFiles += _onGetFiles;
    if (_onGetWork  != null) job.OnGetWork  += _onGetWork;
    if (_onValidate != null) job.OnValidate += _onValidate;
    if (_onFinalize != null) job.OnFinalize += _onFinalize;

    job.JobOptions = _jobOptions;
    job.RenderOptions = _renderOptions;
    job.Settings = _settings;

    await job.Run();
  }
}
