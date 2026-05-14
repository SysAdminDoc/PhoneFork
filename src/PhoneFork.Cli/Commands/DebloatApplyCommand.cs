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
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = devices.FirstOrDefault(d => d.Serial == s.Serial);
        if (picked is null) { AnsiConsole.MarkupLine($"[red]Device {Markup.Escape(s.Serial)} not connected.[/]"); return 1; }

        var dataset = DebloatDataset.Load(log);
        var scanner = new DebloatScanner(host.Client, log, dataset);
        var candidates = await scanner.ScanAsync(picked, ct);

        List<string> queue;
        if (s.Packages.Length > 0)
        {
            queue = s.Packages.ToList();
        }
        else
        {
            var tiers = ProfileTiers(s.Profile, s.IncludeUnsafe);
            queue = candidates
                .Where(c => c.IsEnabled && tiers.Contains(c.Entry.Tier))
                .Select(c => c.Entry.PackageId)
                .ToList();
        }

        AnsiConsole.MarkupLine($"[bold]Queue:[/] {queue.Count} package(s). Profile: {Markup.Escape(s.Profile)} (include-unsafe={s.IncludeUnsafe}).");
        if (s.DryRun) AnsiConsole.MarkupLine("[yellow]Dry-run — no writes.[/]");

        var svc = new DebloatService(host.Client, log);
        var result = await svc.ApplyAsync(picked, queue, s.DryRun,
            new Progress<string>(m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]")), ct);

        AnsiConsole.MarkupLine($"[green]disabled[/] {result.Disabled}, [grey]already disabled[/] {result.AlreadyDisabled}, [red]failed[/] {result.Failed} in {result.Elapsed.TotalSeconds:F1}s.");
        AnsiConsole.MarkupLine($"[grey]Snapshot:[/] {Markup.Escape(result.SnapshotPath)}");
        return result.Failed == 0 ? 0 : 2;
    }

    private static HashSet<DebloatTier> ProfileTiers(string profile, bool includeUnsafe)
    {
        var tiers = profile.ToLowerInvariant() switch
        {
            "conservative" => new HashSet<DebloatTier> { DebloatTier.Delete },
            "recommended"  => new HashSet<DebloatTier> { DebloatTier.Delete, DebloatTier.Replace },
            "aggressive"   => new HashSet<DebloatTier> { DebloatTier.Delete, DebloatTier.Replace, DebloatTier.Caution },
            _              => new HashSet<DebloatTier> { DebloatTier.Delete },
        };
        if (includeUnsafe) tiers.Add(DebloatTier.Unsafe);
        return tiers;
    }
}
