using Vault.Core.Http;
using Vault.Core.IGDB;
using Vault.Core.Jobs;
using Vault.Core.Message;

namespace Vault.Api.Endpoints;

public static class ImportEndpoint {
  public static IEndpointRouteBuilder MapImport(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/import");
    group.MapPost("/", Run);

    return app;
  }

  public static async Task<IResult> Run(ImportRequest request, IgdbService igdbSvc, MessageService messageSvc) {
    var job = ImportJob.Create(request, igdbSvc, messageSvc);
    job.Initialize(messageSvc);
    await job.Run(messageSvc);

    return Results.Ok($"Imported {job.Progress.Processed} Games for: '{request.Console}'");
  }
}
