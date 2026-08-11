using Vault.Core.Jobs;
using Vault.Core.Message;

namespace Vault.Api.Endpoints;

public static class ExportEndpoint {
  public static IEndpointRouteBuilder MapExport(this IEndpointRouteBuilder app) {
    var group = app.MapGroup("/export");
    group.MapPost("/", Run);

    return app;
  }

  public static async Task<IResult> Run(ExportRequest request, MessageService messageSvc) {
    var job = ExportJob.Create(request, messageSvc);
    job.Initialize(messageSvc);
    await job.Run(messageSvc);

    return Results.Ok($"Exported {job.Progress.Processed} Games for: '{request.Console}'");
  }
}
