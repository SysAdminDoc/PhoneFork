using System.ComponentModel;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

/// <summary>
/// <c>phonefork honesty</c> — pre-flight scan of a source device for Samsung
/// categories that won't transfer through the no-root pipeline (F040, F108).
/// </summary>
public sealed class HonestyCommand : AsyncCommand<HonestyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Source device serial to probe.")]
        public required string Serial { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, devices, log) = AdbBootstrap.Initialize();
        var phone = devices.Phones.FirstOrDefault(p => p.Serial == s.Serial);
        if (phone is null)
        {
            AnsiConsole.MarkupLine($"[red]Device not found:[/] {Markup.Escape(s.Serial)}");
            return 2;
        }

        var device = host.GetDevices().FirstOrDefault(d => d.Serial == s.Serial);
        if (device is null)
        {
            AnsiConsole.MarkupLine($"[red]Device offline:[/] {Markup.Escape(s.Serial)}");
            return 2;
        }

        var svc = new SamsungHonestyService(host.Client, log);
        var report = await svc.ProbeAsync(device, ct);

        if (report.Findings.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No Samsung honesty flags detected on this device.[/]");
            return 0;
        }

        var table = new Table().AddColumns("Level", "Title", "Package", "Detail");
        foreach (var f in report.Findings)
        {
            var levelMarkup = f.Level switch
            {
                HonestyLevel.Blocker => "[red]blocker[/]",
                HonestyLevel.Warning => "[yellow]warning[/]",
                _ => "[grey]info[/]",
            };
            table.AddRow(
                levelMarkup,
                Markup.Escape(f.Title),
                Markup.Escape(f.PackageId ?? "—"),
                Markup.Escape(f.Detail));
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine(
            $"[grey]Blockers: {report.BlockerCount}  Warnings: {report.WarningCount}  Info: {report.InfoCount}[/]");
        return report.HasBlockers ? 1 : 0;
    }
}
