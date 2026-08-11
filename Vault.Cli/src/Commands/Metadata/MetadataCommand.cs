using Spectre.Console.Cli;
using Vault.Core.Message;
using Vault.Cli.Renderer;
using Vault.Core.IGDB;
using Vault.Core.Jobs;

namespace Vault.Cli.Commands;

public class MetadataCommand : AsyncCommand<MetadataSettings> {
  private readonly IgdbService _igdbSvc;
  private readonly MessageService _messageSvc;

  public MetadataCommand(IgdbService igdbSvc, MessageService messageSvc) {
    _igdbSvc = igdbSvc;
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, MetadataSettings settings, CancellationToken _cancellationToken) {
    var job = MetadataJob.Create(settings, _igdbSvc, _messageSvc);
    await CommandRenderer.Render(job, _messageSvc, new RenderSettings(true, "Game"));

    return 0;
  }
}
