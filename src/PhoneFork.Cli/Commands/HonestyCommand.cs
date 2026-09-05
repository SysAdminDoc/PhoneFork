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

        // F114 - Advanced Protection blocks package installs and USB data while locked, so it
        // belongs alongside the Samsung honesty flags rather than surfacing as an ADB failure.
        var advancedProtection = await new AdvancedProtectionService(host.Client, log).ProbeAsync(device, ct);
        var findings = report.Findings.Concat(new[] { advancedProtection.ToFinding("selected") }).ToList();

        var table = new Table().AddColumns("Level", "Title", "Package", "Detail");
        foreach (var f in findings)
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
        var blockers = findings.Count(f => f.Level == HonestyLevel.Blocker);
        var warnings = findings.Count(f => f.Level == HonestyLevel.Warning);
        var info = findings.Count(f => f.Level == HonestyLevel.Info);
        AnsiConsole.MarkupLine($"[grey]Blockers: {blockers}  Warnings: {warnings}  Info: {info}[/]");
        return blockers > 0 ? 1 : 0;
    }
}
