using Spectre.Console.Cli;
using Vault.Cli.Renderer;
using Vault.Core.Jobs;
using Vault.Core.Message;

namespace Vault.Cli.Commands;

public class ESDECommand : AsyncCommand<ESDESettings> {
  private readonly MessageService _messageSvc;
  public ESDECommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, ESDESettings settings, CancellationToken _cancellationToken) {
    var job = ESDEJob.Create(settings, _messageSvc);
    await CommandRenderer.Render(job, _messageSvc, new RenderSettings(true, "Console"));

    return 0;
  }
}
