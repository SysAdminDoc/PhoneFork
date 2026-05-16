using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// State of Shizuku on a particular device.
/// </summary>
public enum ShizukuState
{
    /// <summary>Shizuku app is not installed.</summary>
    NotInstalled,
    /// <summary>Installed but the service (rikka.shizuku) is not running.</summary>
    NotRunning,
    /// <summary>Service is reachable; PhoneFork's helper can bind for shell-UID API calls.</summary>
    Running,
    /// <summary>Could not determine — typically because the device was offline mid-probe.</summary>
    Unknown,
}

/// <summary>
/// Detects Shizuku presence/state and emits a runbook for the user (F012).
///
/// Shizuku is the canonical "elevate to shell UID over Wireless ADB without root"
/// primitive on Android 11+. PhoneFork's privileged reads (Wi-Fi PSK exposure via
/// <c>WifiManager.getPrivilegedConfiguredNetworks()</c>, default-role queries) work
/// best when the user runs Shizuku alongside our helper APK. This service does NOT
/// install Shizuku — it only detects, reports state, and emits step-by-step instructions.
/// </summary>
public sealed class ShizukuService
{
    public const string ShizukuPackageId = "moe.shizuku.privileged.api";
    public const string ManagerPackageId = "moe.shizuku.manager"; // legacy / pre-13.4

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public ShizukuService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<ShizukuService>();
    }

    /// <summary>Probe whether Shizuku is installed and its service is running.</summary>
    public async Task<ShizukuState> ProbeAsync(DeviceData device, CancellationToken ct = default)
    {
        try
        {
            var pkgs = await _client.ShellAsync(device,
                $"pm list packages {AdbShell.PackageArg(ShizukuPackageId)}", ct);
            if (!(pkgs ?? "").Contains($"package:{ShizukuPackageId}", StringComparison.Ordinal))
                return ShizukuState.NotInstalled;

            // Shizuku's manager-process registers a system service named "rikka.shizuku".
            // `dumpsys -l` lists all running system services; if rikka.shizuku is there, the
            // process is alive and bindable.
            var services = await _client.ShellAsync(device, "dumpsys -l 2>/dev/null", ct);
            return (services ?? "").Contains("rikka.shizuku", StringComparison.OrdinalIgnoreCase)
                ? ShizukuState.Running
                : ShizukuState.NotRunning;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Shizuku probe failed on {Serial}", device.Serial);
            return ShizukuState.Unknown;
        }
    }

    /// <summary>
    /// Human-readable runbook for the given state. Used by the UI to show the
    /// "Get Wi-Fi PSKs" call-to-action only when it's actionable.
    /// </summary>
    public static string Runbook(ShizukuState state) => state switch
    {
        ShizukuState.Running =>
            "Shizuku is running. PhoneFork's helper APK can bind for privileged reads " +
            "(Wi-Fi PSK export, role queries) without root.",
        ShizukuState.NotRunning =>
            "Shizuku is installed but not running. Open the Shizuku app on the phone, then " +
            "tap Start. On Android 13+ you can enable 'Start on boot (Wireless ADB on trusted Wi-Fi)' " +
            "to skip this step in future migrations.",
        ShizukuState.NotInstalled =>
            "Shizuku is not installed. Download it from F-Droid or the official site " +
            "(https://shizuku.rikka.app/) onto the phone, open the app, then tap Start. " +
            "PhoneFork will detect it automatically.",
        _ => "Could not determine Shizuku state. Ensure the phone is authorized over ADB and try again.",
    };
}
