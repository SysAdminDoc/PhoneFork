using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class MediaSyncCommand : AsyncCommand<MediaSyncCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--from <SERIAL>")]
        [Description("Source device serial.")]
        public required string From { get; init; }

        [CommandOption("--to <SERIAL>")]
        [Description("Destination device serial.")]
        public required string To { get; init; }

        [CommandOption("--categories <CSV>")]
        [Description("Comma-separated MediaCategory names. Default: all categories.")]
        public string? Categories { get; init; }

        [CommandOption("--dry-run")]
        [Description("Manifest + plan only; no writes to destination.")]
        public bool DryRun { get; init; }

        [CommandOption("--delete")]
        [Description("Delete destination-only files (mirror source exactly).")]
        public bool Delete { get; init; }

        [CommandOption("--update")]
        [Description("On conflict, only overwrite if source mtime > dest mtime (rsync --update).")]
        public bool UpdateOnly { get; init; }

        [CommandOption("--preserve-conflicts")]
        [Description("Rename destination conflicts to <name>.sync-conflict-<ts>-<hash>.<ext> before overwriting (Syncthing pattern).")]
        public bool PreserveConflicts { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var src = devices.FirstOrDefault(d => d.Serial == s.From);
        var dst = devices.FirstOrDefault(d => d.Serial == s.To);
        if (src is null) { AnsiConsole.MarkupLine($"[red]Source {Markup.Escape(s.From)} not connected.[/]"); return 1; }
        if (dst is null) { AnsiConsole.MarkupLine($"[red]Destination {Markup.Escape(s.To)} not connected.[/]"); return 1; }
        if (src.Serial == dst.Serial) { AnsiConsole.MarkupLine("[red]Source and destination cannot be the same.[/]"); return 1; }

        var cats = MediaManifestCommand.ParseCategories(s.Categories);
        var manifestSvc = new MediaManifestService(host.Client, log);

        AnsiConsole.MarkupLine("[grey]Building source manifest…[/]");
        var srcManifest = await manifestSvc.BuildAsync(src, cats, ct: ct);
        AnsiConsole.MarkupLine("[grey]Building destination manifest…[/]");
        var dstManifest = await manifestSvc.BuildAsync(dst, cats, ct: ct);

        var plan = MediaDiffer.Build(srcManifest, dstManifest);
        AnsiConsole.MarkupLine($"[bold]Plan:[/] {plan.TotalFilesToTransfer} files, {plan.TotalBytesToTransfer / 1024.0 / 1024.0:F1} MiB, {plan.TotalConflicts} conflicts.");
        if (s.DryRun) AnsiConsole.MarkupLine("[yellow]Dry-run mode — no writes.[/]");

        var syncSvc = new MediaSyncService(host.Client, log);
        MediaSyncResult? result = null;
        await AnsiConsole.Progress()
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Media sync", maxValue: Math.Max(1, plan.TotalFilesToTransfer));
                var progress = new Progress<MediaSyncProgress>(p =>
                {
                    task.Value = p.FilesDone;
                    task.Description = $"[grey]{Markup.Escape(Truncate(p.CurrentRelPath, 60))}[/]";
                });
                result = await syncSvc.ApplyAsync(src, dst, plan, new MediaSyncOptions
                {
                    Delete = s.Delete,
                    UpdateOnly = s.UpdateOnly,
                    PreserveConflicts = s.PreserveConflicts,
                    DryRun = s.DryRun,
                }, progress, ct);
            });

        AnsiConsole.MarkupLine($"[green]pulled[/] {result!.FilesPulled}, [green]pushed[/] {result.FilesPushed}, [grey]skipped[/] {result.FilesSkipped}, [yellow]renamed[/] {result.FilesRenamedAsConflict}, [yellow]deleted[/] {result.FilesDeleted}, [red]errors[/] {result.Errors}.");
        AnsiConsole.MarkupLine($"[grey]Elapsed: {result.Elapsed.TotalSeconds:F1}s[/]");
        return result.Errors == 0 ? 0 : 2;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : "…" + s[^(max - 1)..];
}
