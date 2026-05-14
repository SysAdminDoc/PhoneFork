using CliWrap;
using CliWrap.Buffered;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record AdbPairResult(bool Success, string Output, string Error);
public sealed record AdbConnectResult(bool Success, string Output, string Error);

/// <summary>
/// Drives <c>adb pair</c>, <c>adb connect</c>, and <c>adb disconnect</c> via CliWrap against the
/// bundled <c>tools/adb.exe</c>. AdvancedSharpAdbClient doesn't expose the pairing API directly
/// (the TLS handshake lives in the adb binary itself), so we shell out for this one path only.
/// </summary>
public sealed class AdbPairingService
{
    private readonly string _adbPath;
    private readonly ILogger _log;

    public AdbPairingService(string adbPath, ILogger log)
    {
        _adbPath = adbPath;
        _log = log.ForContext<AdbPairingService>();
    }

    public async Task<AdbPairResult> PairAsync(string hostPort, string pairingCode, CancellationToken ct = default)
    {
        // adb pair <host:port> <pairing_code>
        // Returns "Successfully paired to ..." or "failed to pair to ...".
        var result = await Cli.Wrap(_adbPath)
            .WithArguments(new[] { "pair", hostPort, pairingCode })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);

        var ok = result.StandardOutput.Contains("Successfully paired", StringComparison.OrdinalIgnoreCase);
        _log.Information("adb pair {Host} -> exit={Exit} ok={Ok} out={Out} err={Err}",
            hostPort, result.ExitCode, ok, result.StandardOutput.Trim(), result.StandardError.Trim());
        return new AdbPairResult(ok, result.StandardOutput, result.StandardError);
    }

    public async Task<AdbConnectResult> ConnectAsync(string hostPort, CancellationToken ct = default)
    {
        // adb connect <host:port>
        var result = await Cli.Wrap(_adbPath)
            .WithArguments(new[] { "connect", hostPort })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);
        var ok = result.StandardOutput.Contains("connected", StringComparison.OrdinalIgnoreCase)
                 && !result.StandardOutput.Contains("failed", StringComparison.OrdinalIgnoreCase);
        _log.Information("adb connect {Host} -> exit={Exit} ok={Ok} out={Out}",
            hostPort, result.ExitCode, ok, result.StandardOutput.Trim());
        return new AdbConnectResult(ok, result.StandardOutput, result.StandardError);
    }

    public async Task<AdbConnectResult> DisconnectAsync(string? hostPort = null, CancellationToken ct = default)
    {
        var args = hostPort is null ? new[] { "disconnect" } : new[] { "disconnect", hostPort };
        var result = await Cli.Wrap(_adbPath)
            .WithArguments(args)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);
        return new AdbConnectResult(result.ExitCode == 0, result.StandardOutput, result.StandardError);
    }

    /// <summary>
    /// Parse a "WIFI:T:ADB;S:<svcname>;P:<code>;;" pairing QR string into (svcname, code).
    /// </summary>
    public static (string ServiceName, string Code)? ParsePairingQr(string qrText)
    {
        if (!qrText.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase)) return null;
        string? svc = null, code = null;
        foreach (var part in qrText["WIFI:".Length..].Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = part.IndexOf(':');
            if (colon <= 0) continue;
            var key = part[..colon];
            var val = part[(colon + 1)..];
            switch (key.Trim().ToUpperInvariant())
            {
                case "S": svc = val; break;
                case "P": code = val; break;
            }
        }
        return svc is not null && code is not null ? (svc, code) : null;
    }
}
