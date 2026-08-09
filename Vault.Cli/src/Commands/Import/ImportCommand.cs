using Spectre.Console.Cli;
using Vault.Core.Message;
using Vault.Cli.Renderer;
using Vault.Core.IGDB;
using Vault.Core.Commands;

namespace Vault.Cli.Commands;

public class ImportCommand : AsyncCommand<ImportSettings> {
  private readonly IgdbService _igdbSvc;
  private readonly MessageService _messageSvc;
  
  public ImportCommand(IgdbService igdbSvc, MessageService messageSvc) {
    _igdbSvc = igdbSvc;
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, ImportSettings settings, CancellationToken _cancellationToken) {
    var job = ImportJob.Create(settings.ToOptions(), _igdbSvc, _messageSvc);
    await CommandRenderer.Render(job, _messageSvc, new RenderOptions(true, "Game"));

    return 0;
  }
}
