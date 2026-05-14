using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class CscDiffCommand : AsyncCommand<CscDiffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--from <SERIAL>")] [Description("Source device serial.")]
        public required string From { get; init; }

        [CommandOption("--to <SERIAL>")] [Description("Destination device serial.")]
        public required string To { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var src = devices.FirstOrDefault(d => d.Serial == s.From);
        var dst = devices.FirstOrDefault(d => d.Serial == s.To);
        if (src is null || dst is null) { AnsiConsole.MarkupLine("[red]One or both devices not connected.[/]"); return 1; }

        var svc = new CscDiffService(host.Client, log);
        var src1 = await svc.CaptureAsync(src, ct);
        var dst1 = await svc.CaptureAsync(dst, ct);
        var diff = svc.Diff(src1, dst1);

        var table = new Table().RoundedBorder().AddColumn("Property").AddColumn(s.From).AddColumn(s.To).AddColumn("Match");
        void Row(string label, string a, string b, bool mismatch) =>
            table.AddRow(label, Markup.Escape(a), Markup.Escape(b), mismatch ? "[red]MISMATCH[/]" : "[green]ok[/]");
        Row("Sales code (CSC)", src1.SalesCode, dst1.SalesCode, diff.SalesCodeMismatch);
        Row("Country code",     src1.CountryCode, dst1.CountryCode, diff.CountryCodeMismatch);
        Row("Locale",           src1.Locale, dst1.Locale, diff.LocaleMismatch);
        Row("Timezone",         src1.Timezone, dst1.Timezone, diff.TimezoneMismatch);
        Row("Carrier ISO",      src1.CarrierIso, dst1.CarrierIso, diff.CarrierMismatch);
        AnsiConsole.Write(table);

        if (diff.AnyMismatch)
            AnsiConsole.MarkupLine("[yellow]Mismatches detected. Region-locked items (Samsung Pay tokens, regional Health features) may not restore cleanly.[/]");
        else
            AnsiConsole.MarkupLine("[green]No mismatches. Safe to proceed.[/]");
        return 0;
    }
}
