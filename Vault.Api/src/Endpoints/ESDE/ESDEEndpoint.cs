using Vault.Core.Jobs;
using Vault.Core.Message;

namespace Vault.Api.Endpoints;

public static class ESDEEndpoint {
  public static IEndpointRouteBuilder MapESDE(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/esde");
    group.MapPost("/", Run);

    return app;
  }

  public static async Task<IResult> Run(ESDERequest request, MessageService messageSvc) {
    var job = ESDEJob.Create(request, messageSvc);
    job.Initialize(messageSvc);
    await job.Run(messageSvc);

    return Results.Ok($"Created '/ES-DE' folder.");
  }
}
