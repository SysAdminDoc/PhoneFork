using System.ComponentModel;
using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

/// <summary>
/// <c>phonefork trusted list</c> — show the trusted-pair registry. Raw serials
/// never leave the registry; the table shows hashes and labels only (F004 / F006).
/// </summary>
public sealed class TrustedListCommand : Command
{
    public override int Execute(CommandContext context)
    {
        var log = PhoneFork.Core.Logging.AuditLogger.Create();
        var reg = new TrustedPairRegistry(TrustedPairRegistry.DefaultPath(), log);
        var entries = reg.All;
        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No trusted pairs yet.[/]");
            return 0;
        }
        var table = new Table().AddColumns("Hash", "Label", "Transport", "Last seen", "Last endpoint");
        foreach (var p in entries)
            table.AddRow(
                Markup.Escape(p.SerialHashValue),
                Markup.Escape(p.Label),
                Markup.Escape(p.Transport.ToString()),
                p.LastSeen.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                Markup.Escape(p.LastEndpoint ?? "—"));
        AnsiConsole.Write(table);
        return 0;
    }
}

/// <summary>
/// <c>phonefork trusted forget &lt;hash&gt;</c> — remove an entry. Accepts a hash so the
/// user can copy/paste from `trusted list`; raw serials are never displayed or accepted.
/// </summary>
public sealed class TrustedForgetCommand : Command<TrustedForgetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<HASH>")] [Description("Hashed serial to forget (from `phonefork trusted list`).")]
        public required string Hash { get; init; }
    }

    public override int Execute(CommandContext context, Settings s)
    {
        var log = PhoneFork.Core.Logging.AuditLogger.Create();
        var reg = new TrustedPairRegistry(TrustedPairRegistry.DefaultPath(), log);
        // We don't have the raw serial — find the entry by hash directly.
        var match = reg.All.FirstOrDefault(p => string.Equals(p.SerialHashValue, s.Hash.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            AnsiConsole.MarkupLine($"[grey]No trusted pair matches {Markup.Escape(s.Hash)}.[/]");
            return 1;
        }
        // Forget reads back through SerialHash.Of(raw), so we need the raw serial.
        // We don't have it — but for the CLI we can clear the entry by recreating
        // the registry without it.
        var dropped = ForgetByHash(reg, match.SerialHashValue);
        AnsiConsole.MarkupLine(dropped
            ? $"[green]Forgot {Markup.Escape(match.Label)}.[/]"
            : "[red]Failed to forget the pair.[/]");
        return dropped ? 0 : 2;
    }

    private static bool ForgetByHash(TrustedPairRegistry reg, string hash) => reg.ForgetByHash(hash);
}

/// <summary>
/// <c>phonefork trusted burst {on|off}</c> — toggle ADB burst mode (F104).
/// </summary>
public sealed class BurstModeCommand : Command<BurstModeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<STATE>")] [Description("on | off")]
        public required string State { get; init; }
    }

    public override int Execute(CommandContext context, Settings s)
    {
        var log = PhoneFork.Core.Logging.AuditLogger.Create();
        var svc = new AdbBurstModeService(log);
        bool target;
        switch (s.State.Trim().ToLowerInvariant())
        {
            case "on": case "1": case "true": case "enable": case "enabled": target = true; break;
            case "off": case "0": case "false": case "disable": case "disabled": target = false; break;
            default:
                AnsiConsole.MarkupLine("[red]Pass 'on' or 'off'.[/]");
                return 2;
        }
        var restartRequired = svc.Set(target);
        AnsiConsole.MarkupLine($"ADB burst mode → [yellow]{(target ? "on" : "off")}[/]");
        if (restartRequired)
            AnsiConsole.MarkupLine("[grey]Restart PhoneFork (or `adb kill-server && adb start-server`) for the change to take effect.[/]");
        return 0;
    }
}
