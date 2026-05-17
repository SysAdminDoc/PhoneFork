using System.ComponentModel;
using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class PlatformWatchCommand : AsyncCommand<PlatformWatchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Emit JSON instead of a table.")]
        public bool Json { get; init; }
    }

    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        var report = PlatformMigrationWatcherService.Build();
        if (settings.Json)
        {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(report, MediaJson.Options));
            return Task.FromResult(0);
        }

        var table = new Table().RoundedBorder()
            .AddColumn("Source")
            .AddColumn("Severity")
            .AddColumn("Status")
            .AddColumn("PhoneFork implication");
        foreach (var source in report.Sources)
        {
            table.AddRow(
                Markup.Escape(source.Name),
                SeverityMarkup(source.Severity),
                Markup.Escape(source.Status),
                Markup.Escape(source.PhoneForkImplication));
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]Generated {report.GeneratedAt:u}. Watch={report.WatchCount}, action={report.ActionCount}.[/]");
        foreach (var action in report.RecommendedActions)
            AnsiConsole.MarkupLine($"[yellow]watch[/] {Markup.Escape(action)}");

        return Task.FromResult(0);
    }

    private static string SeverityMarkup(PlatformWatcherSeverity severity) => severity switch
    {
        PlatformWatcherSeverity.Action => "[red]action[/]",
        PlatformWatcherSeverity.Watch => "[yellow]watch[/]",
        _ => "[grey]info[/]",
    };
}
