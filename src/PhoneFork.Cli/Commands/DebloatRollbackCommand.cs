using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class DebloatRollbackCommand : AsyncCommand<DebloatRollbackCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Target device serial.")]
        public required string Serial { get; init; }

        [CommandOption("--snapshot <PATH>")] [Description("Path to a snapshot JSON produced by `debloat apply`.")]
        public required string SnapshotPath { get; init; }

        [CommandOption("--dry-run")] [Description("Print what would be re-enabled; don't write to device.")]
        public bool DryRun { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = devices.FirstOrDefault(d => d.Serial == s.Serial);
        if (picked is null) { AnsiConsole.MarkupLine($"[red]Device {Markup.Escape(s.Serial)} not connected.[/]"); return 1; }

        var svc = new DebloatService(host.Client, log);
        var snap = await svc.LoadSnapshotAsync(s.SnapshotPath, ct);
        if (snap is null) { AnsiConsole.MarkupLine("[red]Snapshot not found / unreadable.[/]"); return 1; }

        AnsiConsole.MarkupLine($"[grey]Snapshot was captured {snap.CapturedAt:u} from {snap.DeviceSerial}; {snap.EnabledSystemPackages.Count} packages were enabled then.[/]");

        var result = await svc.RollbackAsync(picked, snap, s.DryRun,
            new Progress<string>(m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]")), ct);
        AnsiConsole.MarkupLine($"[green]re-enabled[/] {result.ReEnabled}, [grey]already enabled[/] {result.AlreadyEnabled}, [red]failed[/] {result.Failed} in {result.Elapsed.TotalSeconds:F1}s.");
        return result.Failed == 0 ? 0 : 2;
    }
}
