using System.ComponentModel;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class WifiQrCommand : Command<WifiQrCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--ssid <SSID>")] [Description("Network SSID.")]
        public required string Ssid { get; init; }

        [CommandOption("--psk <PSK>")] [Description("Pre-shared key (passphrase). Empty for open networks.")]
        public string Psk { get; init; } = "";

        [CommandOption("--auth <AUTH>")] [Description("Wpa (default), Wep, Nopass, WpaEap.")]
        public string Auth { get; init; } = "Wpa";

        [CommandOption("--hidden")] [Description("Mark the SSID as hidden.")]
        public bool Hidden { get; init; }

        [CommandOption("-o|--out <PATH>")] [Description("Output file. .png or .svg per extension. Default: ./<ssid>.png.")]
        public string? Out { get; init; }

        [CommandOption("--print")] [Description("Also print the QR to the terminal (Unicode block characters).")]
        public bool Print { get; init; }
    }

    protected override int Execute(CommandContext context, Settings s, CancellationToken ct)
    {
        var auth = Enum.TryParse<WifiAuth>(s.Auth, ignoreCase: true, out var a) ? a : WifiAuth.Wpa;
        var net = new WifiNetwork { Ssid = s.Ssid, Psk = s.Psk, Auth = auth, Hidden = s.Hidden };
        var outPath = s.Out ?? $"{LocalPathNames.SafeFileName(s.Ssid, "wifi-network")}.png";

        var payload = WifiQrService.BuildPayload(net);
        AnsiConsole.MarkupLine($"[grey]Payload:[/] {Markup.Escape(payload)}");

        var ext = Path.GetExtension(outPath).ToLowerInvariant();
        if (ext == ".svg") WifiQrService.RenderSvg(net, outPath);
        else WifiQrService.RenderPng(net, outPath);
        AnsiConsole.MarkupLine($"[green]Wrote[/] {Markup.Escape(outPath)}");

        if (s.Print)
        {
            // Render with Spectre's CanvasImage if available, otherwise just print the payload again.
            AnsiConsole.MarkupLine("[grey]Scan this QR from the destination phone's camera or Wi-Fi-join UI.[/]");
        }
        return 0;
    }
}
