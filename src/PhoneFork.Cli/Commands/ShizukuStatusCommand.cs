using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

/// <summary>
/// <c>phonefork shizuku status</c> — detect whether Shizuku is installed and running
/// on a device and emit the appropriate runbook (F012).
/// </summary>
public sealed class ShizukuStatusCommand : AsyncCommand<ShizukuStatusCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Target device serial.")]
        public required string Serial { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var device = host.GetDevices().FirstOrDefault(d => d.Serial == s.Serial);
        if (device is null)
        {
            AnsiConsole.MarkupLine($"[red]Device offline:[/] {Markup.Escape(s.Serial)}");
            return 2;
        }

        var svc = new ShizukuService(host.Client, log);
        var state = await svc.ProbeAsync(device, ct);
        var label = state switch
        {
            ShizukuState.Running     => "[green]running[/]",
            ShizukuState.NotRunning  => "[yellow]not running[/]",
            ShizukuState.NotInstalled => "[grey]not installed[/]",
            _ => "[grey]unknown[/]",
        };
        AnsiConsole.MarkupLine($"Shizuku on {Markup.Escape(s.Serial)}: {label}");
        AnsiConsole.MarkupLine(Markup.Escape(ShizukuService.Runbook(state)));
        return state == ShizukuState.Running ? 0 : 1;
    }
}
