using Vault.Api.Endpoints;
using Vault.Core.IGDB;
using Vault.Core.IGDB.Data;
using Vault.Core.Message;

var builder = WebApplication.CreateBuilder();
builder.Services
  .AddTransient<MessageService>()
  .AddTransient<IgdbService>()
  .AddOptions<IgdbOptions>()
  .Bind(builder.Configuration.GetSection("IGDB"))
  .Validate(options => !string.IsNullOrEmpty(options.ClientId))
  .Validate(Options => !string.IsNullOrEmpty(Options.ClientSecret))
  .ValidateOnStart();

var app = builder.Build();
app.MapOpenApi();
app.MapMetadata();
app.MapGamelist();
app.MapImport();
app.MapExport();
app.MapESDE();
app.Run();
