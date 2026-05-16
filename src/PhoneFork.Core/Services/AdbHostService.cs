using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Owns the lifecycle of the ADB server (port 5037) and exposes a singleton <see cref="IAdbClient"/>.
/// Bundled <c>tools/adb.exe</c> is always preferred over whatever happens to be on PATH.
///
/// Per-install ADB key (F002): before starting the server we point <c>HOME</c> at a
/// PhoneFork-local directory so adbd's "trust this host" key is scoped to this app
/// install. Without this, every PhoneFork instance reuses the user's global
/// <c>%USERPROFILE%\.android\adbkey</c>, which would expose any process that can read
/// the user profile to the same Wireless ADB trust set.
/// </summary>
public sealed class AdbHostService : IDisposable
{
    private readonly ILogger _log;
    private readonly string _adbPath;
    private readonly string _keyDir;
    private bool _started;

    public IAdbClient Client { get; }
    public DeviceMonitor? Monitor { get; private set; }
    public string AdbPath => _adbPath;

    /// <summary>The per-install ADB key directory (parent of <c>.android/</c>).</summary>
    public string KeyDirectory => _keyDir;

    public AdbHostService(string adbExePath, ILogger log, string? keyDir = null)
    {
        _log = log.ForContext<AdbHostService>();
        _adbPath = adbExePath;
        _keyDir = keyDir ?? DefaultKeyDirectory();
        Client = new AdbClient();
    }

    public static string DefaultKeyDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhoneFork", "adb-home");

    public bool EnsureServerRunning()
    {
        if (_started) return true;

        if (RequiresExistingFile(_adbPath) && !File.Exists(_adbPath))
        {
            _log.Error("Bundled adb.exe not found at {Path}", _adbPath);
            return false;
        }

        try
        {
            ConfigureAdbKeyDirectory();

            var server = new AdbServer();
            var result = server.StartServer(_adbPath, restartServerIfNewer: false);
            _log.Information("ADB server start: {Result} (adb={Adb}, keys={KeyDir})", result, _adbPath, _keyDir);

            Monitor = new DeviceMonitor(new AdbSocket(Client.EndPoint));
            Monitor.Start();
            _started = true;
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to start ADB server");
            return false;
        }
    }

    /// <summary>
    /// Set <c>HOME</c> (and <c>ANDROID_SDK_HOME</c>, which some adb builds honor) to the
    /// per-install key directory, and create the <c>.android</c> sub-folder if missing.
    /// adbd reads <c>HOME/.android/adbkey[.pub]</c> on startup and generates a pair if absent.
    /// </summary>
    private void ConfigureAdbKeyDirectory()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(_keyDir, ".android"));
            Environment.SetEnvironmentVariable("HOME", _keyDir);
            Environment.SetEnvironmentVariable("ANDROID_SDK_HOME", _keyDir);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not set per-install ADB key directory; falling back to global ~/.android.");
        }
    }

    public IEnumerable<DeviceData> GetDevices() =>
        _started ? Client.GetDevices() : Enumerable.Empty<DeviceData>();

    public void Dispose()
    {
        try { Monitor?.Dispose(); } catch { }
    }

    private static bool RequiresExistingFile(string adbPath) =>
        Path.IsPathFullyQualified(adbPath)
        || adbPath.Contains(Path.DirectorySeparatorChar)
        || adbPath.Contains(Path.AltDirectorySeparatorChar);
}
