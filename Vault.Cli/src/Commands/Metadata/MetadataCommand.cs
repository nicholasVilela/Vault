using Spectre.Console.Cli;
using Vault.Core.Message;
using Vault.Cli.Renderer;
using Vault.Core.IGDB;
using Vault.Core.Commands;

namespace Vault.Cli.Commands;

public class MetadataCommand : AsyncCommand<MetadataSettings> {
  private readonly IgdbService _igdbSvc;
  private readonly MessageService _messageSvc;

  public MetadataCommand(IgdbService igdbSvc, MessageService messageSvc) {
    _igdbSvc = igdbSvc;
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, MetadataSettings settings, CancellationToken _cancellationToken) {
    var job = MetadataJob.Create(settings.ToOptions(), _igdbSvc, _messageSvc);
    await JobRenderer.Render(job, _messageSvc, new RenderOptions(true, "Game"));

    return 0;
  }
}
