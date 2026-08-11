using Spectre.Console;
using Spectre.Console.Rendering;
using Vault.Core.Jobs;
using Vault.Core.Message;

namespace Vault.Cli.Renderer;

public static class CommandRenderer {
  public async static Task Render<T>(JobRunner<T> job, MessageService messageSvc, RenderSettings settings) where T : IJobSettings {
    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new ElapsedTimeColumn()
        )
      .UseRenderHook((renderable, tasks) =>
        RenderHook(
          job.Progress,
          job.Settings,
          renderable,
          settings,
          messageSvc
        ))
      .StartAsync(async ctx => {
        job.Initialize(messageSvc);

        var masterTask = ctx.AddTask(
          "Master",
          autoStart: true,
          maxValue: job.Progress.TotalWork
        );

        var jobTask = job.Run(messageSvc);
        while (!jobTask.IsCompleted) masterTask.Value = job.Progress.CompletedWork;

        await jobTask;
        masterTask.Value = job.Progress.CompletedWork;
      }
    );

    job.OnFinalize?.Invoke();
  }

  public static IRenderable RenderHook(
    JobProgress jobProgress,
    IJobSettings jobSettings,
    IRenderable renderable,
    RenderSettings renderSettings,
    MessageService messageSvc
  ) {
    var width = 40;
    var title = RenderTitle(jobSettings, width);
    var info = RenderInfo(jobProgress, jobSettings, renderSettings, width);
    var progress = RenderProgress(renderable, width);
    var warnings = RenderWarnings(messageSvc);
    var errors = RenderErrors(messageSvc);

    var main = new Rows(
      title,
      info,
      progress
    );

    var layout = new Grid()
      .AddColumn(new GridColumn())
      .AddColumn(new GridColumn().Width(0))
      .AddColumn(new GridColumn())
      .AddRow(
        main,
        Text.Empty,
        new Rows(errors, warnings)
      );

    return layout;
  }

  private static Panel RenderTitle(IJobSettings settings, int width) {
    var grid =  new Grid()
      .AddColumn(new GridColumn().NoWrap())
      .AddRow(
        Align.Center(new Markup($"[bold][white]{settings.Title}[/][/]"))
      ).Expand();

    var panel = new Panel(new Rows(grid))
    .Header("[bold] COMMAND [/]")
    .RoundedBorder();

    panel.Width = width;

    return panel;
  }

  private static Panel RenderInfo(JobProgress progress, IJobSettings jobSettings, RenderSettings renderSettings, int width) {
    var gameLabel = string.IsNullOrEmpty(renderSettings.Suffix) ? "" : progress.FileCount == 1 ? renderSettings.Suffix: $"{renderSettings.Suffix}s";
    var grid =  new Grid()
      .AddColumn(new GridColumn().PadLeft(0))
      .AddColumn(new GridColumn().PadLeft(1))
      .AddRow(
        new Markup($"[grey]Processed:[/]"),
        new Markup($"[cyan]{progress.Processed}/{progress.FileCount}[/] {(renderSettings.DisplayPlatform ? $"[green]{jobSettings.Console}[/] " : "")}{gameLabel}")
      )
      .AddRow(
        new Markup($"[grey]Skipped:[/]"),
        new Markup($"[cyan]{progress.Skipped}[/]")
      )
      .AddRow(
        new Markup("[grey]Name:[/]"),
        new Markup($"[yellow]{jobSettings.Name ?? "*"}[/]")
      )
      .AddRow(
        new Markup("[grey]Region:[/]"),
        new Markup($"[yellow]{jobSettings.Region}[/]")
      )
      .AddRow(
        new Markup("[grey]Version:[/]"),
        new Markup($"[yellow]{jobSettings.Version}[/]")
      )
      .AddRow(
        new Markup("[grey]Output:[/]"),
        new Markup($"[green]{jobSettings.WritePath}[/]")
      );

    var panel = new Panel(new Rows(grid))
      .Header("[bold] INFO [/]")
      .RoundedBorder();

    panel.Width = width;

    return panel;
  }

  private static Panel RenderProgress(IRenderable renderable, int width) {
    var panel = new Panel(new Rows(renderable))
      .Header("[bold] PROGRESS [/]")
      .RoundedBorder();

    panel.Width = width;

    return panel;
  }

  private static Renderable RenderWarnings(MessageService messageSvc) {
    var warnings = messageSvc.Warnings;
    if (warnings.IsEmpty) return new Markup("");

    var rows = warnings
      .Select(warning => (IRenderable)new Markup($"[white]{warning}[/]"))
      .ToArray();

    var panel = new Panel(new Rows(rows))
      .Header("[bold] WARNINGS [/]")
      .RoundedBorder()
      .BorderColor(Color.Yellow);

    return panel;
  }

  private static Renderable RenderErrors(MessageService messageSvc) {
    var errors = messageSvc.Errors;
    if (errors.IsEmpty) return new Markup("");

    var rows = errors
      .Select(error => (IRenderable)new Markup($"[white]{error}[/]"))
      .ToArray();

    var panel = new Panel(new Rows(rows))
      .Header("[bold] ERRORS [/]")
      .RoundedBorder()
      .BorderColor(Color.Red);

    return panel;
  }
}
