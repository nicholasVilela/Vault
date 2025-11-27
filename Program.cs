using Spectre.Console.Cli;
using Vault.Commands;

class Program {
  static int Main(string[] args) {
    var app = new CommandApp();
    app.Configure(config => {
      config.SetApplicationName("vault");
      config.AddCommand<ImportCommand>("import");
      config.AddCommand<ExportCommand>("export");
    });

    return app.Run(args);
  }
}
