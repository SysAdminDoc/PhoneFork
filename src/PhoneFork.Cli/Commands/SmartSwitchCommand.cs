using PhoneFork.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace PhoneFork.Cli.Commands;

/// <summary>
/// <c>phonefork smartswitch detect</c> — probe both Smart Switch install paths
/// (legacy MSI + Microsoft Store) plus the default backup folder (F024).
/// </summary>
public sealed class SmartSwitchDetectCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var result = SmartSwitchDetection.Probe();
        var label = result.Install switch
        {
            SmartSwitchInstall.NotInstalled => "[grey]not installed[/]",
            SmartSwitchInstall.LegacyMsi => "[yellow]legacy MSI[/]",
            SmartSwitchInstall.MicrosoftStore => "[green]Microsoft Store[/]",
            SmartSwitchInstall.Both => "[yellow]both legacy MSI + Microsoft Store[/]",
            _ => "[grey]unknown[/]",
        };
        AnsiConsole.MarkupLine($"Smart Switch: {label}");
        if (result.LegacyInstallDir is not null)
            AnsiConsole.MarkupLine($"  Legacy install: {Markup.Escape(result.LegacyInstallDir)}");
        if (result.StorePackageDir is not null)
            AnsiConsole.MarkupLine($"  Store package: {Markup.Escape(result.StorePackageDir)}");
        if (result.BackupRoot is not null)
            AnsiConsole.MarkupLine($"  Backup root: {Markup.Escape(result.BackupRoot)}");
        else
            AnsiConsole.MarkupLine("  Backup root: [grey]not found[/] (~\\Documents\\Samsung\\SmartSwitch missing)");
        return result.IsAvailable ? 0 : 1;
    }
}
