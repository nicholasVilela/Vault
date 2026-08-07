using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Vault.Commands;
using Vault.Message;
using Vault.IGDB;
using Vault.IGDB.Data;
using Vault.Http;

class Program {
  static int Main(string[] args) {
    Console.OutputEncoding = Encoding.UTF8;

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

    var app = new CommandApp(new TypeRegistrar(services));
    app.Configure(config => {
      config.SetApplicationName("vault");
      config.AddCommand<ImportCommand>("import");
      config.AddCommand<ExportCommand>("export");
      config.AddCommand<GameListCommand>("gamelist");
      config.AddCommand<MetadataCommand>("metadata");
      config.AddCommand<ESDECommand>("esde");
    });

    return app.Run(args);
  }
}
