using System.ComponentModel;
using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class SettingsDumpCommand : AsyncCommand<SettingsDumpCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-d|--device <SERIAL>")]
        [Description("Device serial.")]
        public string? Serial { get; init; }

        [CommandOption("-o|--out <PATH>")]
        [Description("Output path for snapshot JSON. Default: ./settings-<serial>-<ts>.json.")]
        public string? Out { get; init; }

        [CommandOption("--namespaces <CSV>")]
        [Description("Comma-separated namespace names (secure, system, global). Default: all.")]
        public string? Namespaces { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (host, _, log) = AdbBootstrap.Initialize();
        var devices = host.GetDevices().ToList();
        var picked = s.Serial is { Length: > 0 } serial
            ? devices.FirstOrDefault(d => d.Serial == serial)
            : (devices.Count == 1 ? devices[0] : null);
        if (picked is null) { AnsiConsole.MarkupLine("[red]Specify --device <serial>.[/]"); return 1; }

        var ns = ParseNamespaces(s.Namespaces);
        var svc = new SettingsSnapshotService(host.Client, log);
        var snap = await svc.CaptureAsync(picked, ns, new Progress<string>(m => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(m)}[/]")), ct);

        var outPath = s.Out ?? $"settings-{picked.Serial}-{DateTime.UtcNow:yyyyMMddTHHmmss}.json";
        await using var fs = File.Create(outPath);
        await JsonSerializer.SerializeAsync(fs, snap, MediaJson.Options, ct);
        AnsiConsole.MarkupLine($"[green]Wrote {Markup.Escape(outPath)}[/] — {snap.TotalKeyCount} keys across {snap.Namespaces.Count} namespaces.");
        return 0;
    }

    internal static List<SettingsNamespace> ParseNamespaces(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new List<SettingsNamespace> { SettingsNamespace.Secure, SettingsNamespace.System, SettingsNamespace.Global };
        var picked = new List<SettingsNamespace>();
        foreach (var token in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<SettingsNamespace>(token, ignoreCase: true, out var n))
                picked.Add(n);
            else
                AnsiConsole.MarkupLine($"[yellow]Unknown namespace '{Markup.Escape(token)}', skipped.[/]");
        }
        return picked;
    }
}
