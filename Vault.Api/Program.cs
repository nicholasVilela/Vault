using Vault.Api.Endpoints;
using Vault.Core.Http;
using Vault.Core.IGDB;
using Vault.Core.IGDB.Data;
using Vault.Core.Message;

var builder = WebApplication.CreateBuilder();
builder.Services
  .AddOptions<IgdbOptions>()
  .Bind(builder.Configuration.GetSection("IGDB"))
  .Validate(options => !string.IsNullOrEmpty(options.ClientId))
  .Validate(Options => !string.IsNullOrEmpty(Options.ClientSecret))
  .ValidateOnStart();

builder.Services.AddTransient<IgdbService>();
builder.Services.AddTransient<MessageService>();

var app = builder.Build();
app.MapOpenApi();
app.MapMetadata();
app.MapGamelist();
app.MapImport();
app.Run();
