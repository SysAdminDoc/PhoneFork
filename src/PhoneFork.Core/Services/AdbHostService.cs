using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Owns the lifecycle of the ADB server (port 5037) and exposes a singleton <see cref="IAdbClient"/>.
/// Bundled <c>tools/adb.exe</c> is always preferred over whatever happens to be on PATH.
/// </summary>
public sealed class AdbHostService : IDisposable
{
    private readonly ILogger _log;
    private readonly string _adbPath;
    private bool _started;

    public IAdbClient Client { get; }
    public DeviceMonitor? Monitor { get; private set; }
    public string AdbPath => _adbPath;

    public AdbHostService(string adbExePath, ILogger log)
    {
        _log = log.ForContext<AdbHostService>();
        _adbPath = adbExePath;
        Client = new AdbClient();
    }

    public bool EnsureServerRunning()
    {
        if (_started) return true;

        if (!File.Exists(_adbPath))
        {
            _log.Error("Bundled adb.exe not found at {Path}", _adbPath);
            return false;
        }

        try
        {
            var server = new AdbServer();
            var result = server.StartServer(_adbPath, restartServerIfNewer: false);
            _log.Information("ADB server start: {Result} (adb={Adb})", result, _adbPath);

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

    public IEnumerable<DeviceData> GetDevices() =>
        _started ? Client.GetDevices() : Enumerable.Empty<DeviceData>();

    public void Dispose()
    {
        try { Monitor?.Dispose(); } catch { }
    }
}
