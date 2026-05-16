using CliWrap;
using CliWrap.Buffered;
using Serilog;
using System.Text;

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
    /// Run <c>adb mdns services</c> and return the discovered LAN entries (F005).
    /// </summary>
    public async Task<IReadOnlyList<MdnsService>> ListMdnsServicesAsync(CancellationToken ct = default)
    {
        var result = await Cli.Wrap(_adbPath)
            .WithArguments(new[] { "mdns", "services" })
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct);

        var services = ParseMdnsServices(result.StandardOutput);
        _log.Information("adb mdns services -> exit={Exit} services={Count}",
            result.ExitCode, services.Count);
        return services;
    }

    /// <summary>
    /// Parses the line-based output of <c>adb mdns services</c>. Format per row:
    /// <c>&lt;instance&gt;\t&lt;service&gt;\t&lt;host:port&gt;</c>. The first banner line
    /// ("List of discovered mdns services") and blank lines are ignored.
    /// </summary>
    public static IReadOnlyList<MdnsService> ParseMdnsServices(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return Array.Empty<MdnsService>();

        var services = new List<MdnsService>();
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("List of", StringComparison.OrdinalIgnoreCase)) continue;

            var fields = line.Split('\t', StringSplitOptions.None);
            if (fields.Length < 3) continue;

            var instance = fields[0].Trim();
            var service = fields[1].Trim();
            var endpoint = fields[2].Trim();
            if (string.IsNullOrEmpty(endpoint)) continue;

            services.Add(new MdnsService(instance, service, endpoint));
        }
        return services;
    }

    /// <summary>
    /// Parse a "WIFI:T:ADB;S:&lt;svcname&gt;;P:&lt;code&gt;;;" pairing QR string into (svcname, code).
    /// </summary>
    public static (string ServiceName, string Code)? ParsePairingQr(string qrText)
    {
        if (string.IsNullOrWhiteSpace(qrText)) return null;
        qrText = qrText.Trim();
        if (!qrText.StartsWith("WIFI:", StringComparison.OrdinalIgnoreCase)) return null;
        string? svc = null, code = null;
        foreach (var part in SplitWifiFields(qrText["WIFI:".Length..]))
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

    private static IEnumerable<string> SplitWifiFields(string body)
    {
        var current = new StringBuilder();
        var escaped = false;

        foreach (var ch in body)
        {
            if (escaped)
            {
                current.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == ';')
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                continue;
            }

            current.Append(ch);
        }

        if (escaped)
            current.Append('\\');

        if (current.Length > 0)
            yield return current.ToString();
    }
}

/// <summary>
/// One entry returned by <c>adb mdns services</c>.
/// </summary>
/// <param name="Instance">The mDNS instance name (typically the device serial / friendly name).</param>
/// <param name="ServiceType">e.g. <c>_adb-tls-connect._tcp</c> or <c>_adb-tls-pairing._tcp</c>.</param>
/// <param name="HostPort">The <c>host:port</c> tuple for connect/pair.</param>
public sealed record MdnsService(string Instance, string ServiceType, string HostPort)
{
    public bool IsConnect => ServiceType.Contains("connect", StringComparison.OrdinalIgnoreCase);
    public bool IsPairing => ServiceType.Contains("pairing", StringComparison.OrdinalIgnoreCase);
}
