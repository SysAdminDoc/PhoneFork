using System.Text.RegularExpressions;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Best-effort SSID enumeration without root or a privileged helper APK. Reads
/// <c>cmd wifi list-networks</c> and falls back to parsing <c>dumpsys wifi</c> for SSIDs.
/// PSKs are NOT recoverable through this path — Android 13+ redacts them from
/// <c>dumpsys</c>, and <c>WifiManager.getPrivilegedConfiguredNetworks()</c> requires either
/// Shizuku elevation or a system-signed app (both deferred to v0.7 helper APK).
/// </summary>
public sealed class WifiSnapshotService
{
    private static readonly Regex ListNetworksRow = new(
        @"^\s*(?<id>\d+)\s+(?<ssid>.+?)\s+(?<security>\S+)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex DumpsysNetworkLine = new(
        @"^\s*SSID=""(?<ssid>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public WifiSnapshotService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<WifiSnapshotService>();
    }

    public async Task<IReadOnlyList<WifiNetwork>> ListSsidsAsync(DeviceData device, CancellationToken ct = default)
    {
        // Primary: `cmd wifi list-networks` (Android 10+).
        var listOut = await _client.ShellAsync(device, "cmd wifi list-networks", ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<WifiNetwork>();

        foreach (var rawLine in (listOut ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd('\r');
            var m = ListNetworksRow.Match(line);
            if (!m.Success) continue;
            var ssid = m.Groups["ssid"].Value.Trim();
            if (ssid is "Network Id" or "" ) continue; // skip header
            if (!seen.Add(ssid)) continue;
            var sec = m.Groups["security"].Value;
            var auth = sec.Contains("WPA3", StringComparison.OrdinalIgnoreCase) || sec.Contains("WPA2", StringComparison.OrdinalIgnoreCase) || sec.Contains("WPA", StringComparison.OrdinalIgnoreCase)
                ? WifiAuth.Wpa
                : sec.Contains("WEP", StringComparison.OrdinalIgnoreCase)
                    ? WifiAuth.Wep
                    : sec.Contains("EAP", StringComparison.OrdinalIgnoreCase)
                        ? WifiAuth.WpaEap
                        : WifiAuth.Nopass;
            result.Add(new WifiNetwork { Ssid = ssid, Auth = auth, SourceSerial = device.Serial });
        }

        if (result.Count == 0)
        {
            // Fallback: dumpsys wifi (older Android versions or carrier builds where list-networks is blocked).
            var dumpsysOut = await _client.ShellAsync(device, "dumpsys wifi", ct);
            foreach (Match m in DumpsysNetworkLine.Matches(dumpsysOut ?? ""))
            {
                var ssid = m.Groups["ssid"].Value;
                if (!seen.Add(ssid)) continue;
                result.Add(new WifiNetwork { Ssid = ssid, SourceSerial = device.Serial });
            }
        }

        _log.Information("Wi-Fi SSIDs on {Serial}: {Count} (PSKs not recoverable without helper APK)", device.Serial, result.Count);
        return result;
    }
}
