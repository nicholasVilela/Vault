using Vault.Core.Http;
using Vault.Core.IGDB;
using Vault.Core.Jobs;
using Vault.Core.Message;

namespace Vault.Api.Endpoints;

public static class GamelistEndpoint {
  public static IEndpointRouteBuilder MapGamelist(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/gamelist");
    group.MapPost("/", Run);

    return app;
  }

  public static async Task<IResult> Run(GamelistRequest request, MessageService messageSvc) {
    using var httpSvc = new HttpService(4, 1, 8);
    var job = GamelistJob.Create(request, messageSvc, httpSvc);
    job.Initialize(messageSvc);
    await job.Run(messageSvc);

    return Results.Ok($"Updated Gamelist for {request.Console}");
  }
}
