using System.ComponentModel;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class DebloatListCommand : AsyncCommand<DebloatListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Device serial.")]
        public string? Serial { get; init; }

        [CommandOption("--tier <TIER>")] [Description("Filter by safety tier (Delete, Replace, Caution, Unsafe). Repeatable via CSV.")]
        public string? Tier { get; init; }

        [CommandOption("--list <LIST>")] [Description("Filter by source list (Oem, Google, Carrier, Aosp, Misc). Repeatable via CSV.")]
        public string? List { get; init; }

        [CommandOption("--include-disabled")] [Description("Also show already-disabled rows (default: only currently enabled).")]
        public bool IncludeDisabled { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = s.Serial is { Length: > 0 } x
            ? devices.FirstOrDefault(d => d.Serial == x)
            : (devices.Count == 1 ? devices[0] : null);
        if (picked is null) { AnsiConsole.MarkupLine("[red]Specify --device <serial>.[/]"); return 1; }

        var dataset = DebloatDataset.Load(log);
        var scanner = new DebloatScanner(host.Client, log, dataset);
        var candidates = await scanner.ScanAsync(picked, ct);

        var tiers = ParseEnums<DebloatTier>(s.Tier);
        var lists = ParseEnums<DebloatList>(s.List);
        var filtered = candidates.AsEnumerable();
        if (!s.IncludeDisabled) filtered = filtered.Where(c => c.IsEnabled);
        if (tiers.Count > 0) filtered = filtered.Where(c => tiers.Contains(c.Entry.Tier));
        if (lists.Count > 0) filtered = filtered.Where(c => lists.Contains(c.Entry.List));
        var rows = filtered.OrderBy(c => c.Entry.PackageId, StringComparer.Ordinal).ToList();

        var table = new Table().RoundedBorder()
            .AddColumn("Package").AddColumn("Label").AddColumn("Tier").AddColumn("List").AddColumn("Enabled");
        foreach (var c in rows)
        {
            table.AddRow(
                Markup.Escape(c.Entry.PackageId),
                Markup.Escape(c.Entry.DisplayLabel),
                c.Entry.Tier.ToString(),
                c.Entry.List.ToString(),
                c.IsEnabled ? "[green]y[/]" : "[grey]-[/]");
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{rows.Count} of {candidates.Count} matched on {picked.Serial}.[/]");
        AnsiConsole.MarkupLine($"  by tier: Delete={candidates.Count(c => c.Entry.Tier == DebloatTier.Delete)}  Replace={candidates.Count(c => c.Entry.Tier == DebloatTier.Replace)}  Caution={candidates.Count(c => c.Entry.Tier == DebloatTier.Caution)}  Unsafe={candidates.Count(c => c.Entry.Tier == DebloatTier.Unsafe)}");
        return 0;
    }

    internal static HashSet<T> ParseEnums<T>(string? csv) where T : struct, Enum
    {
        var set = new HashSet<T>();
        if (string.IsNullOrWhiteSpace(csv)) return set;
        foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<T>(token, true, out var v)) set.Add(v);
        return set;
    }
}
