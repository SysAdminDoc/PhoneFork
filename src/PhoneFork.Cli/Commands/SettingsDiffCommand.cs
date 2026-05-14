using System.ComponentModel;
using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class SettingsDiffCommand : AsyncCommand<SettingsDiffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--src <PATH>")] [Description("Source snapshot JSON path.")]
        public required string SrcSnapshot { get; init; }

        [CommandOption("--dst <PATH>")] [Description("Destination snapshot JSON path.")]
        public required string DstSnapshot { get; init; }

        [CommandOption("--show-different")] [Description("Print the keys that differ (key=src->dst).")]
        public bool ShowDifferent { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var src = await Read(s.SrcSnapshot, ct);
        var dst = await Read(s.DstSnapshot, ct);
        if (src is null || dst is null) { AnsiConsole.MarkupLine("[red]Could not read both snapshots.[/]"); return 1; }
        var plan = SettingsDiffer.Build(src, dst);

        var table = new Table().RoundedBorder()
            .AddColumn("Namespace").AddColumn("Only src").AddColumn("Different").AddColumn("Same").AddColumn("Only dst").AddColumn("Applicable");
        foreach (var nd in plan.Namespaces)
        {
            table.AddRow(
                nd.Namespace.ToString(),
                nd.Count(SettingsDiffOutcome.OnlyOnSource).ToString(),
                nd.Count(SettingsDiffOutcome.Different).ToString(),
                nd.Count(SettingsDiffOutcome.Same).ToString(),
                nd.Count(SettingsDiffOutcome.OnlyOnDest).ToString(),
                (nd.Count(SettingsDiffOutcome.Different) + nd.Count(SettingsDiffOutcome.OnlyOnSource)).ToString());
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[bold]{plan.TotalApplicable} keys[/] can be applied from source to dest.");

        if (s.ShowDifferent)
        {
            foreach (var nd in plan.Namespaces)
            {
                foreach (var e in nd.Entries.Where(e => e.Outcome == SettingsDiffOutcome.Different))
                    AnsiConsole.MarkupLine($"[grey]{nd.Namespace.ToString().ToLowerInvariant()}[/] {Markup.Escape(e.Key)} = [red]{Markup.Escape(Trim(e.DestValue ?? ""))}[/] -> [green]{Markup.Escape(Trim(e.SourceValue ?? ""))}[/]");
            }
        }
        return 0;
    }

    private static async Task<SettingsSnapshot?> Read(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) { AnsiConsole.MarkupLine($"[red]Not found: {Markup.Escape(path)}[/]"); return null; }
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SettingsSnapshot>(fs, MediaJson.Options, ct);
    }

    private static string Trim(string s) => s.Length <= 80 ? s : s[..77] + "…";
}
