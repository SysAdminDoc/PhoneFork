using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class WifiListCommand : AsyncCommand<WifiListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")] [Description("Device serial.")]
        public string? Serial { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = s.Serial is { Length: > 0 } x
            ? devices.FirstOrDefault(d => d.Serial == x)
            : (devices.Count == 1 ? devices[0] : null);
        if (picked is null) { AnsiConsole.MarkupLine("[red]Specify --device <serial>.[/]"); return 1; }

        var svc = new WifiSnapshotService(host.Client, log);
        var nets = await svc.ListSsidsAsync(picked, ct);

        var table = new Table().RoundedBorder().AddColumn("SSID").AddColumn("Auth").AddColumn("Hidden");
        foreach (var n in nets.OrderBy(n => n.Ssid, StringComparer.OrdinalIgnoreCase))
            table.AddRow(Markup.Escape(n.Ssid), n.Auth.ToString(), n.Hidden ? "y" : "-");
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{nets.Count} SSID(s). PSKs are NOT recoverable without a Shizuku-bound helper APK (v0.7).[/]");
        AnsiConsole.MarkupLine("[grey]Use `phonefork wifi qr` to render a join-QR by manually entering the PSK.[/]");
        return 0;
    }
}
