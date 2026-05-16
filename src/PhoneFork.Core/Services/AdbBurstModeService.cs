using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Per-process ADB Burst Mode toggle (F104). Burst Mode is exposed by
/// platform-tools 36+ (we bundle 37.0.0) and accelerates large-file transfers
/// on high-latency links by sending multiple packets before acknowledgment.
/// It's opt-in because some marginal USB cables get worse with it enabled.
///
/// Toggle works by setting the <c>ADB_BURST_MODE</c> environment variable
/// before the adb server is launched (or before any per-process `adb` invocation).
/// Setting it after the server has started has no effect — the host must restart.
/// </summary>
public sealed class AdbBurstModeService
{
    private const string EnvVarName = "ADB_BURST_MODE";
    private readonly ILogger _log;

    public AdbBurstModeService(ILogger log)
    {
        _log = log.ForContext<AdbBurstModeService>();
    }

    /// <summary>True iff burst mode is currently requested for newly-spawned ADB processes.</summary>
    public bool IsBurstEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnvVarName), "1", StringComparison.Ordinal);

    /// <summary>
    /// Enable burst mode for the next ADB server start. Returns true if the caller
    /// also needs to restart the ADB server for the change to take effect (i.e. the
    /// variable was not already set to the desired state).
    /// </summary>
    public bool Enable()
    {
        var before = IsBurstEnabled;
        Environment.SetEnvironmentVariable(EnvVarName, "1");
        _log.Information("ADB burst mode -> enabled (restartRequired={Restart})", !before);
        return !before;
    }

    /// <summary>Disable burst mode for the next ADB server start.</summary>
    public bool Disable()
    {
        var before = IsBurstEnabled;
        Environment.SetEnvironmentVariable(EnvVarName, null);
        _log.Information("ADB burst mode -> disabled (restartRequired={Restart})", before);
        return before;
    }

    /// <summary>
    /// Convenience: set burst mode in a single call. Use this from the WPF settings
    /// pane so the toggle button is a one-shot.
    /// </summary>
    public bool Set(bool enabled) => enabled ? Enable() : Disable();
}
