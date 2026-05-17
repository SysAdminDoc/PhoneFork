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

        [CommandOption("--checkpoint <PATH>")]
        [Description("Resume checkpoint JSON path. Default: stage/media-sync-checkpoint.json.")]
        public string? CheckpointPath { get; init; }

        [CommandOption("--report <PATH>")]
        [Description("Evidence report JSON path. Default: stage/media-sync-report-<timestamp>.json.")]
        public string? ReportPath { get; init; }

        [CommandOption("--max-attempts <N>")]
        [Description("Pull/push attempts per file before marking failed. Default: 3.")]
        public int MaxAttempts { get; init; } = 3;

        [CommandOption("--defer-quick-share")]
        [Description("Record single-large-file Quick Share candidates as user-deferred instead of transferring them.")]
        public bool DeferQuickShare { get; init; }
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
        foreach (var advisory in MediaSyncEvidence.BuildAdvisories(plan))
            AnsiConsole.MarkupLine($"[yellow]{advisory.Kind}[/] {Markup.Escape(advisory.RelPath)} ({advisory.SizeBytes / 1024.0 / 1024.0:F1} MiB): {Markup.Escape(advisory.Detail)}");
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
                    var speed = p.BytesPerSecond <= 0 ? "" : $" {p.BytesPerSecond / 1024.0 / 1024.0:F1} MiB/s";
                    task.Description = $"[grey]{Markup.Escape(Truncate(p.CurrentRelPath, 54))}{speed}[/]";
                });
                result = await syncSvc.ApplyAsync(src, dst, plan, new MediaSyncOptions
                {
                    Delete = s.Delete,
                    UpdateOnly = s.UpdateOnly,
                    PreserveConflicts = s.PreserveConflicts,
                    DryRun = s.DryRun,
                    CheckpointPath = s.CheckpointPath,
                    ReportPath = s.ReportPath,
                    MaxAttempts = s.MaxAttempts,
                    DeferQuickShareCandidates = s.DeferQuickShare,
                }, progress, ct);
            });

        AnsiConsole.MarkupLine($"[green]pulled[/] {result!.FilesPulled}, [green]pushed[/] {result.FilesPushed}, [grey]skipped[/] {result.FilesSkipped}, [yellow]retried[/] {result.FilesRetried}, [yellow]deferred[/] {result.FilesDeferred}, [yellow]renamed[/] {result.FilesRenamedAsConflict}, [yellow]deleted[/] {result.FilesDeleted}, [red]errors[/] {result.Errors}.");
        if (!string.IsNullOrWhiteSpace(result.CheckpointPath))
            AnsiConsole.MarkupLine($"[grey]Checkpoint:[/] {Markup.Escape(result.CheckpointPath)}");
        if (!string.IsNullOrWhiteSpace(result.ReportPath))
            AnsiConsole.MarkupLine($"[grey]Report:[/] {Markup.Escape(result.ReportPath)}");
        var receiptArtifacts = new List<MigrationReceiptArtifact>();
        if (!string.IsNullOrWhiteSpace(result.CheckpointPath))
            receiptArtifacts.Add(new MigrationReceiptArtifact("checkpoint", result.CheckpointPath));
        if (!string.IsNullOrWhiteSpace(result.ReportPath))
            receiptArtifacts.Add(new MigrationReceiptArtifact("evidence-report", result.ReportPath));
        var receiptPath = await new MigrationReceiptService(log).WriteAsync(
            MigrationReceiptService.Create(
                operation: "media-sync",
                dryRun: s.DryRun,
                devices: new[]
                {
                    MigrationReceiptService.Device("source", src),
                    MigrationReceiptService.Device("destination", dst),
                },
                categories: new[]
                {
                    MigrationReceiptService.Category(
                        "media",
                        planned: plan.TotalFilesToTransfer + (s.Delete ? plan.CategoryDiffs.Sum(c => c.Count(PhoneFork.Core.Services.MediaDiffOutcome.NewOnDest)) : 0),
                        succeeded: result.FilesPushed + result.FilesDeleted,
                        skipped: result.FilesSkipped + result.FilesDeferred,
                        failed: result.Errors,
                        warnings: result.Advisories.Select(a => $"{a.Kind}: {a.RelPath} - {a.Detail}"),
                        artifacts: receiptArtifacts),
                },
                warnings: result.Advisories.Select(a => a.Detail),
                artifacts: receiptArtifacts),
            ct);
        AnsiConsole.MarkupLine($"[grey]Receipt:[/] {Markup.Escape(receiptPath)}");
        AnsiConsole.MarkupLine($"[grey]Elapsed: {result.Elapsed.TotalSeconds:F1}s[/]");
        return result.Errors == 0 ? 0 : 2;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : "…" + s[^(max - 1)..];
}
