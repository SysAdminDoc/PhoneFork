using System.ComponentModel;
using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class SettingsApplyCommand : AsyncCommand<SettingsApplyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--from <SERIAL>")] [Description("Source device serial (snapshot is captured live).")]
        public required string From { get; init; }

        [CommandOption("--to <SERIAL>")] [Description("Destination device serial.")]
        public required string To { get; init; }

        [CommandOption("--namespaces <CSV>")] [Description("Namespaces to consider (secure, system, global). Default: all.")]
        public string? Namespaces { get; init; }

        [CommandOption("--keys <CSV>")] [Description("Optional explicit key allowlist. Default: every Different + OnlyOnSource key minus the safety blocklist.")]
        public string? Keys { get; init; }

        [CommandOption("--include-only-on-source")] [Description("Also push keys that exist only on the source (default skips them — safer).")]
        public bool IncludeOnlyOnSource { get; init; }

        [CommandOption("--dry-run")] [Description("Print what would change; don't write to destination.")]
        public bool DryRun { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var src = devices.FirstOrDefault(d => d.Serial == s.From);
        var dst = devices.FirstOrDefault(d => d.Serial == s.To);
        if (src is null || dst is null) { AnsiConsole.MarkupLine("[red]One or both devices not connected.[/]"); return 1; }

        var ns = SettingsDumpCommand.ParseNamespaces(s.Namespaces);
        var snapSvc = new SettingsSnapshotService(host.Client, log);
        AnsiConsole.MarkupLine("[grey]Capturing source snapshot…[/]");
        var srcSnap = await snapSvc.CaptureAsync(src, ns, ct: ct);
        AnsiConsole.MarkupLine("[grey]Capturing destination snapshot…[/]");
        var dstSnap = await snapSvc.CaptureAsync(dst, ns, ct: ct);

        var plan = SettingsDiffer.Build(srcSnap, dstSnap);
        var allowedKeys = s.Keys is { Length: > 0 }
            ? s.Keys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal)
            : null;

        var entries = plan.Namespaces.SelectMany(nd => nd.Entries
            .Where(e => e.Outcome == SettingsDiffOutcome.Different
                        || (s.IncludeOnlyOnSource && e.Outcome == SettingsDiffOutcome.OnlyOnSource))
            .Where(e => allowedKeys is null || allowedKeys.Contains(e.Key))).ToList();

        AnsiConsole.MarkupLine($"[bold]{entries.Count} keys[/] queued for apply.");
        if (s.DryRun) AnsiConsole.MarkupLine("[yellow]Dry-run — no writes.[/]");

        var apply = new SettingsApplyService(host.Client, log);
        var result = await apply.ApplyAsync(dst, entries, s.DryRun,
            new Progress<string>(msg => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(msg)}[/]")), ct);

        AnsiConsole.MarkupLine($"[green]applied[/] {result.Applied}, [grey]skipped[/] {result.Skipped}, [red]failed[/] {result.Failed} in {result.Elapsed.TotalSeconds:F1}s.");
        if (result.Failed > 0)
        {
            var t = new Table().RoundedBorder().AddColumn("Ns").AddColumn("Key").AddColumn("Error");
            foreach (var f in result.Failures.Take(20))
                t.AddRow(f.Ns.ToString(), Markup.Escape(f.Key), Markup.Escape(f.Error));
            AnsiConsole.Write(t);
        }
        return result.Failed == 0 ? 0 : 2;
    }
}
