using System.ComponentModel;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class DebloatApplyCommand : AsyncCommand<DebloatApplyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Target device serial (the one being debloated).")]
        public required string Serial { get; init; }

        [CommandOption("--profile <NAME>")] [Description("Conservative (Delete only), Recommended (Delete+Replace), Aggressive (Delete+Replace+Caution). Default: Conservative.")]
        public string Profile { get; init; } = "Conservative";

        [CommandOption("--include-unsafe")] [Description("Also include Unsafe tier rows. Will likely break the device. Off by default.")]
        public bool IncludeUnsafe { get; init; }

        [CommandOption("--package <PKG>")] [Description("Explicit package allowlist (repeatable). Overrides --profile.")]
        public string[] Packages { get; init; } = Array.Empty<string>();

        [CommandOption("--dry-run")] [Description("Print what would change; don't disable anything.")]
        public bool DryRun { get; init; }

        [CommandOption("--overlay-feed <PATH>")]
        [Description("Checksummed out-of-band debloat override feed JSON.")]
        public string? OverlayFeed { get; init; }

        [CommandOption("--overlay-sha256 <SHA256>")]
        [Description("Expected SHA-256 for --overlay-feed. If omitted, <feed>.sha256 is required.")]
        public string? OverlaySha256 { get; init; }

        [CommandOption("--allow-multi-user")]
        [Description("Proceed even when the destination has work profiles or secondary users. PhoneFork still targets Android user 0 only.")]
        public bool AllowMultiUser { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = devices.FirstOrDefault(d => d.Serial == s.Serial);
        if (picked is null) { AnsiConsole.MarkupLine($"[red]Device {Markup.Escape(s.Serial)} not connected.[/]"); return 1; }

        var dataset = await DebloatDatasetResolver.LoadForDeviceAsync(
            host.Client, picked, log, s.OverlayFeed, s.OverlaySha256, ct);
        var scanner = new DebloatScanner(host.Client, log, dataset);
        var candidates = await scanner.ScanAsync(picked, ct);

        List<string> queue;
        if (s.Packages.Length > 0)
        {
            // An explicit package list bypasses tier filtering, so gate the Unsafe tier here
            // too rather than letting a named package slip past the profile guard (F110).
            var named = s.Packages.ToList();
            var unsafeNamed = DebloatProfiles.UnsafePackagesIn(dataset, named);
            if (unsafeNamed.Count > 0 && !s.IncludeUnsafe)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Refusing to disable {unsafeNamed.Count} package(s) the dataset rates Unsafe:[/] " +
                    Markup.Escape(string.Join(", ", unsafeNamed)));
                AnsiConsole.MarkupLine("[yellow]Pass --include-unsafe if you accept the risk.[/]");
                return 2;
            }
            queue = named;
        }
        else
        {
            var tiers = DebloatProfiles.TiersFor(s.Profile, s.IncludeUnsafe);
            queue = candidates
                .Where(c => c.IsEnabled && tiers.Contains(c.Entry.Tier))
                .Select(c => c.Entry.PackageId)
                .ToList();
        }

        AnsiConsole.MarkupLine($"[bold]Queue:[/] {queue.Count} package(s). Profile: {Markup.Escape(s.Profile)} (include-unsafe={s.IncludeUnsafe}).");

        // F121 - upstream records which packages depend on each entry. Show the ones that will
        // stay enabled while something they need is disabled, before anything is written.
        var dependencyWarnings = DebloatDependencyCheck.Evaluate(dataset, queue, candidates);
        if (dependencyWarnings.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{dependencyWarnings.Count} queued package(s) are needed by something that stays enabled:[/]");
            foreach (var warning in dependencyWarnings)
                AnsiConsole.MarkupLine($"  [yellow]-[/] {Markup.Escape(warning.Describe())}");
        }

        if (s.DryRun) AnsiConsole.MarkupLine("[yellow]Dry-run — no writes.[/]");

        var svc = new DebloatService(host.Client, log);
        var result = await svc.ApplyAsync(picked, queue, s.DryRun,
            new Progress<string>(m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]")), ct,
            allowMultiUser: s.AllowMultiUser);

        AnsiConsole.MarkupLine($"[green]disabled[/] {result.Disabled}, [grey]already disabled[/] {result.AlreadyDisabled}, [red]failed[/] {result.Failed} in {result.Elapsed.TotalSeconds:F1}s.");
        if (s.DryRun)
            AnsiConsole.MarkupLine("[grey]No snapshot written during dry-run.[/]");
        else
            AnsiConsole.MarkupLine($"[grey]Snapshot:[/] {Markup.Escape(result.SnapshotPath)}");
        var artifacts = string.IsNullOrWhiteSpace(result.SnapshotPath)
            ? Array.Empty<MigrationReceiptArtifact>()
            : new[] { new MigrationReceiptArtifact("rollback-snapshot", result.SnapshotPath) };
        var receiptPath = await new MigrationReceiptService(log).WriteAsync(
            MigrationReceiptService.Create(
                operation: "debloat-apply",
                dryRun: s.DryRun,
                devices: new[] { MigrationReceiptService.Device("destination", picked) },
                categories: new[]
                {
                    MigrationReceiptService.Category(
                        "debloat",
                        planned: queue.Count,
                        succeeded: result.Disabled,
                        skipped: result.AlreadyDisabled,
                        failed: result.Failed,
                        failureDetails: result.Results.Where(r => !r.Success).Select(r => $"{r.PackageId}: {r.Output}"),
                        artifacts: artifacts),
                },
                warnings: (s.IncludeUnsafe ? new[] { "Unsafe debloat tier was included by explicit CLI option." } : Array.Empty<string>())
                    .Concat(dependencyWarnings.Select(w => w.Describe()))
                    .ToArray(),
                artifacts: artifacts),
            ct);
        AnsiConsole.MarkupLine($"[grey]Receipt:[/] {Markup.Escape(receiptPath)}");
        return result.Failed == 0 ? 0 : 2;
    }
}
