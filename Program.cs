using System.Text;
using Spectre.Console.Cli;
using Vault.Commands;

class Program {
  static int Main(string[] args) {
    Console.OutputEncoding = Encoding.UTF8;
    var app = new CommandApp();
    app.Configure(config => {
      config.SetApplicationName("vault");
      config.AddCommand<ImportCommand>("import");
      config.AddCommand<ExportCommand>("export");
      config.AddCommand<GameListCommand>("gamelist");
      config.AddCommand<MetadataCommand>("metadata");
    });

    return app.Run(args);
  }
}
