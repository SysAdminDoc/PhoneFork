using PhoneFork.Core.Logging;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.Cli;

/// <summary>
/// Locates the bundled <c>tools/adb.exe</c> relative to the CLI executable and starts the ADB server.
/// Falls back to <c>%LOCALAPPDATA%\PhoneFork\tools\adb.exe</c>, then to <c>PATH</c>.
/// </summary>
internal static class AdbBootstrap
{
    public static (AdbHostService Host, DeviceService Devices, ILogger Log) Initialize()
    {
        var log = AuditLogger.Create();
        var adbPath = ResolveAdb();
        var host = new AdbHostService(adbPath, log);
        if (!host.EnsureServerRunning())
            throw new InvalidOperationException(
                $"Could not start ADB server with bundled adb at {adbPath}. Make sure 'tools/adb.exe' ships next to phonefork.exe.");
        var dev = new DeviceService(host, log);
        dev.Refresh();
        return (host, dev, log);
    }

    private static string ResolveAdb()
    {
        var here = Path.GetDirectoryName(AppContext.BaseDirectory) ?? Environment.CurrentDirectory;
        var candidates = new[]
        {
            Path.Combine(here, "tools", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "adb.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhoneFork", "tools", "adb.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        // Last resort: assume on PATH.
        return "adb.exe";
    }
}
