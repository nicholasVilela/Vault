using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Cli;
using Vault.Core.Message;
using System.Xml.Linq;
using Vault.Core.Http;
using Vault.Core.Commands;
using Vault.Cli.Renderer;

namespace Vault.Cli.Commands;

public class GamelistCommand : AsyncCommand<GamelistSettings> {
  private readonly MessageService _messageSvc;

  public GamelistCommand(MessageService messageSvc) {
    _messageSvc = messageSvc;
  }

  public override async Task<int> ExecuteAsync(CommandContext context, GamelistSettings settings, CancellationToken _cancellationToken) {
    using var httpSvc = new HttpService(4, 1, 8);
    var job = GamelistJob.CreateJob(settings.ToOptions(), _messageSvc, httpSvc);
    await JobRenderer.Render(job, _messageSvc, new RenderOptions(true, "Game"));

    return 0;
  }
}
