using Spectre.Console.Cli;
using Vault.Core.Message;
using Vault.Cli.Renderer;
using Vault.Core.Jobs;

namespace Vault.Cli.Commands;

public class ExportCommand : AsyncCommand<ExportSettings> {
  private readonly MessageService _messageSvc;

  public ExportCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken _cancellationToken) {
    var job = ExportJob.Create(settings, _messageSvc);
    await CommandRenderer.Render(job, _messageSvc, new RenderSettings(true, "Game"));

    return 0;
  }
}
