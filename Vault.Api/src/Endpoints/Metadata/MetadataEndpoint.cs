using Vault.Core.IGDB;
using Vault.Core.Jobs;
using Vault.Core.Message;

namespace Vault.Api.Endpoints;

public static class MetadataEndpoint {
  public static IEndpointRouteBuilder MapMetadata(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/metadata");
    group.MapPost("/", Run);

    return app;
  }

  public static async Task<IResult> Run(MetadataRequest request, IgdbService igdbSvc, MessageService messageSvc) {
    var job = MetadataJob.Create(request, igdbSvc, messageSvc);
    job.Initialize(messageSvc);
    await job.Run(messageSvc);

    return Results.Ok($"Updated Metadata for {request.Console}");
  }
}
