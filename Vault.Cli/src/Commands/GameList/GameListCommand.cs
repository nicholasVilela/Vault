using Spectre.Console.Cli;
using Vault.Core.Message;
using Vault.Core.Http;
using Vault.Core.Jobs;
using Vault.Cli.Renderer;

namespace Vault.Cli.Commands;

public class GamelistCommand : AsyncCommand<GamelistSettings> {
  private readonly MessageService _messageSvc;

  public GamelistCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, GamelistSettings settings, CancellationToken _cancellationToken) {
    using var httpSvc = new HttpService(4, 1, 8);
    var job = GamelistJob.Create(settings, _messageSvc, httpSvc);
    await CommandRenderer.Render(job, _messageSvc, new RenderSettings(true, "Game"));

    return 0;
  }
}
