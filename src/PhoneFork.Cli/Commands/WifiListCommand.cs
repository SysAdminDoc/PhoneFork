using System.ComponentModel;
using PhoneFork.Core.Models;
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

        [CommandOption("--with-psk")]
        [Description("Also read saved pre-shared keys through the app_process agent (Android 11+). Keys are printed to the console only.")]
        public bool WithPsk { get; init; }

        [CommandOption("--show-psk")]
        [Description("Print the keys in full instead of masking them. Requires --with-psk.")]
        public bool ShowPsk { get; init; }

        [CommandOption("--jar <PATH>")]
        [Description("Path to phonefork-agent.jar. Defaults to assets/helper/phonefork-agent.jar next to the CLI.")]
        public string? JarPath { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = s.Serial is { Length: > 0 } x
            ? devices.FirstOrDefault(d => d.Serial == x)
            : (devices.Count == 1 ? devices[0] : null);
        if (picked is null) { AnsiConsole.MarkupLine("[red]Specify --device <serial>.[/]"); return 1; }

        IReadOnlyList<WifiNetwork> nets;
        var pskAvailable = false;

        if (s.WithPsk)
        {
            // F116 - the agent runs as the shell user, which Android 11+ allows to call
            // getPrivilegedConfiguredNetworks(). Fall back with a stated reason if it cannot.
            var jar = s.JarPath ?? HelperAgentCommand.ResolveAgentJar();
            var exporter = new WifiPskExportService(new AppProcessAgentService(host.Client, log), log);
            var result = await exporter.ExportAsync(picked, jar, keepAgent: false, ct);

            if (result.Success)
            {
                nets = result.Networks;
                pskAvailable = true;
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Privileged Wi-Fi read unavailable:[/] {Markup.Escape(result.Error ?? "unknown")}");
                AnsiConsole.MarkupLine("[yellow]Falling back to SSID-only enumeration.[/]");
                nets = await new WifiSnapshotService(host.Client, log).ListSsidsAsync(picked, ct);
            }
        }
        else
        {
            nets = await new WifiSnapshotService(host.Client, log).ListSsidsAsync(picked, ct);
        }

        var table = new Table().RoundedBorder().AddColumn("SSID").AddColumn("Auth").AddColumn("Hidden");
        if (pskAvailable) table.AddColumn("Key");

        foreach (var n in nets.OrderBy(n => n.Ssid, StringComparer.OrdinalIgnoreCase))
        {
            var row = new List<string>
            {
                Markup.Escape(n.Ssid),
                n.Auth.ToString(),
                n.Hidden ? "y" : "-",
            };
            if (pskAvailable)
                row.Add(Markup.Escape(FormatPsk(n.Psk, s.ShowPsk)));
            table.AddRow(row.ToArray());
        }
        AnsiConsole.Write(table);

        if (pskAvailable)
        {
            var withKeys = nets.Count(n => !string.IsNullOrEmpty(n.Psk));
            AnsiConsole.MarkupLine($"[grey]{nets.Count} saved network(s), {withKeys} with a recoverable key.[/]");
            if (!s.ShowPsk)
                AnsiConsole.MarkupLine("[grey]Keys are masked. Pass --show-psk to print them in full.[/]");
            AnsiConsole.MarkupLine("[grey]Keys are never written to the audit log or a receipt.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[grey]{nets.Count} SSID(s). Pass --with-psk to read saved keys through the app_process agent.[/]");
            AnsiConsole.MarkupLine("[grey]Use `phonefork wifi qr` to render a join-QR by manually entering the PSK.[/]");
        }
        return 0;
    }

    /// <summary>
    /// Masks a key for console display: enough to recognise which network it belongs to,
    /// not enough to read the secret over someone's shoulder.
    /// </summary>
    internal static string FormatPsk(string? psk, bool show)
    {
        if (string.IsNullOrEmpty(psk)) return "-";
        if (show) return psk;
        return psk.Length <= 4 ? new string('*', psk.Length) : $"{psk[..2]}{new string('*', psk.Length - 4)}{psk[^2..]}";
    }
}
