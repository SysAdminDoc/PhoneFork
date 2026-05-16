using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

/// <summary>
/// <c>phonefork mdns services</c> — list wireless ADB services discovered on the LAN
/// via mDNS-SD (F005). Trust state is read from the local trusted-pair registry; raw
/// serials are never displayed.
/// </summary>
public sealed class MdnsServicesCommand : AsyncCommand<MdnsServicesCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (_, _, log) = AdbBootstrap.Initialize();
        var svc = new AdbPairingService(PairCommand.ResolveAdb(), log);
        var trusted = new TrustedPairRegistry(TrustedPairRegistry.DefaultPath(), log);

        var services = await svc.ListMdnsServicesAsync(ct);
        if (services.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No wireless ADB services discovered. Verify Wireless debugging is enabled on the phone and both hosts share the LAN.[/]");
            return 0;
        }

        var table = new Table().AddColumns("Endpoint", "Service", "Instance", "Trust");
        foreach (var entry in services)
        {
            var trust = trusted.IsTrusted(entry.HostPort) ? "[green]trusted[/]" : "[yellow]new[/]";
            table.AddRow(
                Markup.Escape(entry.HostPort),
                Markup.Escape(entry.ServiceType),
                Markup.Escape(string.IsNullOrEmpty(entry.Instance) ? "—" : entry.Instance),
                trust);
        }
        AnsiConsole.Write(table);
        return 0;
    }
}
