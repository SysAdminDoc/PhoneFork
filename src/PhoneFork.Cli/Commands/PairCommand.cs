using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

public sealed class PairCommand : AsyncCommand<PairCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<HOSTPORT>")] [Description("ip:port shown on the phone's Wireless debugging > Pair device with pairing code screen.")]
        public required string HostPort { get; init; }

        [CommandArgument(1, "<CODE>")] [Description("Six-digit pairing code shown on the phone.")]
        public required string Code { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (_, _, log) = AdbBootstrap.Initialize();
        var adb = ResolveAdb();
        var svc = new AdbPairingService(adb, log);
        AnsiConsole.MarkupLine($"[grey]adb pair {Markup.Escape(s.HostPort)} <code>[/]");
        var result = await svc.PairAsync(s.HostPort, s.Code, ct);
        AnsiConsole.WriteLine(result.Output.Trim());
        if (!string.IsNullOrWhiteSpace(result.Error)) AnsiConsole.MarkupLine($"[red]{Markup.Escape(result.Error.Trim())}[/]");
        AnsiConsole.MarkupLine(result.Success ? "[green]Paired.[/] Run `phonefork connect <host:port>` to attach the daemon."
                                              : "[red]Pair failed.[/]");
        return result.Success ? 0 : 2;
    }

    internal static string ResolveAdb()
    {
        var here = Path.GetDirectoryName(AppContext.BaseDirectory) ?? Environment.CurrentDirectory;
        var candidates = new[]
        {
            Path.Combine(here, "tools", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "adb.exe"),
        };
        return candidates.FirstOrDefault(File.Exists) ?? "adb.exe";
    }
}

public sealed class ConnectCommand : AsyncCommand<ConnectCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<HOSTPORT>")] [Description("ip:port of the paired phone's wireless ADB endpoint.")]
        public required string HostPort { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (_, _, log) = AdbBootstrap.Initialize();
        var svc = new AdbPairingService(PairCommand.ResolveAdb(), log);
        var result = await svc.ConnectAsync(s.HostPort, ct);
        AnsiConsole.WriteLine(result.Output.Trim());
        if (!string.IsNullOrWhiteSpace(result.Error)) AnsiConsole.MarkupLine($"[red]{Markup.Escape(result.Error.Trim())}[/]");
        return result.Success ? 0 : 2;
    }
}

public sealed class DisconnectCommand : AsyncCommand<DisconnectCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[HOSTPORT]")] [Description("Optional ip:port. Omit to disconnect every wireless device.")]
        public string? HostPort { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings s, CancellationToken ct)
    {
        var (_, _, log) = AdbBootstrap.Initialize();
        var svc = new AdbPairingService(PairCommand.ResolveAdb(), log);
        var result = await svc.DisconnectAsync(s.HostPort, ct);
        AnsiConsole.WriteLine(result.Output.Trim());
        return result.Success ? 0 : 2;
    }
}
