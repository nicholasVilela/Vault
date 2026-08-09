using Spectre.Console.Cli;
using Vault.Core.Message;
using Vault.Cli.Renderer;
using Vault.Core.Commands;

namespace Vault.Cli.Commands;

public class ExportCommand : AsyncCommand<ExportSettings> {
  private readonly MessageService _messageSvc;

  public ExportCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken _cancellationToken) {
    var job = ExportJob.Create(settings.ToOptions(), _messageSvc);
    await CommandRenderer.Render(job, _messageSvc, new RenderOptions(true, "Game"));

    return 0;
  }
}
