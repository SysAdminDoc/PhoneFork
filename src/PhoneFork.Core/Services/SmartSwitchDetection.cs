using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PhoneFork.Core.Services;

/// <summary>
/// Where on disk Smart Switch lives. Per the 2026 distribution-channel change, the
/// legacy MSI install ships under <c>Program Files (x86)</c> while the new Microsoft
/// Store distribution drops a sandboxed package under <c>%LocalAppData%\Packages</c>.
/// The same WPF binary runs in both cases, but file paths and update channels differ,
/// so PhoneFork's Smart Switch handoff (F025) needs to know which is installed.
/// </summary>
public enum SmartSwitchInstall
{
    NotInstalled,
    LegacyMsi,
    MicrosoftStore,
    Both,
}

/// <summary>Probed result for the Smart Switch installation on the current host.</summary>
public sealed record SmartSwitchDetectionResult(
    SmartSwitchInstall Install,
    string? LegacyInstallDir,
    string? StorePackageDir,
    string? BackupRoot)
{
    public bool IsAvailable => Install != SmartSwitchInstall.NotInstalled;
}

/// <summary>
/// Pure-managed Windows host detector for Samsung Smart Switch (F024). Probes both
/// the legacy MSI install location and the Microsoft Store sandboxed package
/// directory, plus the user's Smart Switch backup folder under <c>Documents\Samsung\SmartSwitch</c>.
/// </summary>
public static class SmartSwitchDetection
{
    private const string LegacyRegKey = @"SOFTWARE\WOW6432Node\Samsung\Samsung Smart Switch";
    private const string LegacyDefaultDir = @"C:\Program Files (x86)\Samsung\Smart Switch PC";

    /// <summary>Run the probe and return what was found.</summary>
    public static SmartSwitchDetectionResult Probe()
    {
        // The probe only makes sense on Windows. Returning NotInstalled on non-Windows
        // hosts means callers can wire this into cross-platform code without guarding
        // every call site individually.
        if (!OperatingSystem.IsWindows())
            return new SmartSwitchDetectionResult(SmartSwitchInstall.NotInstalled, null, null, null);

        var legacy = ResolveLegacy();
        var store = ResolveMicrosoftStore();
        var backupRoot = ResolveBackupRoot();

        var install = (legacy is not null, store is not null) switch
        {
            (true, true) => SmartSwitchInstall.Both,
            (true, false) => SmartSwitchInstall.LegacyMsi,
            (false, true) => SmartSwitchInstall.MicrosoftStore,
            (false, false) => SmartSwitchInstall.NotInstalled,
        };

        return new SmartSwitchDetectionResult(install, legacy, store, backupRoot);
    }

    [SupportedOSPlatform("windows")]
    private static string? ResolveLegacy()
    {
        try
        {
            // Try the 64-bit registry view first (Windows-on-Windows redirects 32-bit installers
            // into WOW6432Node).
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = hklm.OpenSubKey(LegacyRegKey);
            if (key?.GetValue("InstallPath") is string path && Directory.Exists(path))
                return path;

            if (Directory.Exists(LegacyDefaultDir))
                return LegacyDefaultDir;
        }
        catch
        {
            // Registry access can fail silently under low-privilege contexts; fall through.
        }
        return null;
    }

    private static string? ResolveMicrosoftStore()
    {
        try
        {
            var packagesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages");
            if (!Directory.Exists(packagesRoot)) return null;

            // The Store package name starts with Samsung's publisher id. Match conservatively
            // so a future Samsung-published replacement that uses a different package name
            // surfaces as Unknown rather than getting silently mis-detected.
            var hit = Directory.EnumerateDirectories(packagesRoot, "SamsungElectronicsCo*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(d => d.Contains("SmartSwitch", StringComparison.OrdinalIgnoreCase));
            return hit;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveBackupRoot()
    {
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(docs)) return null;

            var root = Path.Combine(docs, "Samsung", "SmartSwitch");
            return Directory.Exists(root) ? root : null;
        }
        catch
        {
            return null;
        }
    }
}
