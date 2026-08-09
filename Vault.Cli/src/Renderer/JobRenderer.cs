using Spectre.Console;
using Spectre.Console.Rendering;
using Vault.Cli.Commands;
using Vault.Core.Commands;
using Vault.Core.Job;
using Vault.Core.Message;

namespace Vault.Cli.Renderer;

public static class JobRenderer {
  public async static Task Render<T>(JobDispatcher<T> job, MessageService messageSvc, RenderOptions options) where T : BaseOptions {
    await AnsiConsole.Progress()
      .Columns(
        new ProgressBarColumn(),
        new PercentageColumn(),
        new RemainingTimeColumn()
        )
      .UseRenderHook((renderable, tasks) =>
        RenderHook(
          job.Progress,
          job.Options,
          renderable,
          options,
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
        var completedWork = 0L;

        while (!jobTask.IsCompleted) {
          var progress = job.Progress;
          var increment = progress.CompletedWork - completedWork;

          if (increment > 0) {
            masterTask.Increment(increment);
            completedWork = progress.CompletedWork;
          }
        }

        await jobTask;

        var finalProgress = job.Progress;
        var finalIncrement = finalProgress.CompletedWork - completedWork;

        if (finalIncrement > 0) {
          masterTask.Increment(finalIncrement);
        }
      }
    );

    job.OnFinalize?.Invoke();
  }

  public static IRenderable RenderHook(
    JobProgress jobProgress,
    BaseOptions jobOptions,
    IRenderable renderable,
    RenderOptions renderOptions,
    MessageService messageSvc
  ) {
    var width = 40;
    var title = RenderTitle(jobOptions, width);
    var info = RenderInfo(jobProgress, jobOptions, renderOptions, width);
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

  private static Panel RenderTitle(BaseOptions settings, int width) {
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

  private static Panel RenderInfo(JobProgress progress, BaseOptions jobOptions, RenderOptions options, int width) {
    var gameLabel = string.IsNullOrEmpty(options.Suffix) ? "" : progress.FileCount == 1 ? options.Suffix: $"{options.Suffix}s";
    var grid =  new Grid()
      .AddColumn(new GridColumn().PadLeft(0))
      .AddColumn(new GridColumn().PadLeft(1))
      .AddRow(
        new Markup($"[grey]Processed:[/]"),
        new Markup($"[cyan]{progress.Processed}/{progress.FileCount}[/] {(options.DisplayPlatform ? $"[green]{jobOptions.Console}[/] " : "")}{gameLabel}")
      )
      .AddRow(
        new Markup($"[grey]Skipped:[/]"),
        new Markup($"[cyan]{progress.Skipped}[/]")
      )
      .AddRow(
        new Markup("[grey]Name:[/]"),
        new Markup($"[yellow]{jobOptions.Name ?? "*"}[/]")
      )
      .AddRow(
        new Markup("[grey]Region:[/]"),
        new Markup($"[yellow]{jobOptions.Region}[/]")
      )
      .AddRow(
        new Markup("[grey]Version:[/]"),
        new Markup($"[yellow]{jobOptions.Version}[/]")
      )
      .AddRow(
        new Markup("[grey]Output:[/]"),
        new Markup($"[green]{jobOptions.WritePath}[/]")
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
