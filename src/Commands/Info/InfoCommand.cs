using System.Collections.Concurrent;
using System.IO.Compression;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace Vault.Commands;

public class InfoCommand : AsyncCommand<ExportSettings> {
  public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings, CancellationToken _cancellationToken) {
    return 0;
  }
}
