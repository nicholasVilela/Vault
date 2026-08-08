using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Vault.Cli;
using Vault.Cli.Commands;
using Vault.Core.Message;
using Vault.Core.IGDB;
using Vault.Core.IGDB.Data;

class Program {
  static int Main(string[] args) {
    var services = GetServices();

    var app = new CommandApp(new TypeRegistrar(services));
    app.Configure(config => {
      config.SetApplicationName("vault");
      config.AddCommand<ImportCommand>("import");
      config.AddCommand<ExportCommand>("export");
      config.AddCommand<GamelistCommand>("gamelist");
      config.AddCommand<MetadataCommand>("metadata");
      config.AddCommand<ESDECommand>("esde");
    });

    return app.Run(args);
  }

  public static ServiceCollection GetServices() {
    var configuration = new ConfigurationBuilder()
      .AddEnvironmentVariables()
      .Build();

    var services = new ServiceCollection();
    services
      .AddSingleton<IConfiguration>(configuration)
      .AddOptions<IgdbOptions>()
      .Bind(configuration.GetSection("IGDB"))
      .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId))
      .Validate(options => !string.IsNullOrWhiteSpace(options.ClientSecret))
      .ValidateOnStart();

    services.AddTransient<IgdbService>();
    services.AddTransient<MessageService>();

    return services;
  }
}
