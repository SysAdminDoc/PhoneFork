using System.Text.Json;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Outcome of a privileged Wi-Fi export (F116). <see cref="Networks"/> carries secrets, so this
/// record is never logged or serialized into a receipt; only <see cref="Summary"/> is safe to show.
/// </summary>
public sealed record WifiPskExportResult(
    IReadOnlyList<WifiNetwork> Networks,
    string? Error)
{
    public bool Success => Error is null;

    public int WithPskCount => Networks.Count(n => !string.IsNullOrEmpty(n.Psk));

    /// <summary>Secret-free one-liner safe for logs, receipts and the UI status bar.</summary>
    public string Summary => Success
        ? $"{Networks.Count} saved network(s), {WithPskCount} with a recoverable key"
        : $"privileged Wi-Fi export unavailable: {Error}";
}

/// <summary>
/// Reads saved Wi-Fi networks including pre-shared keys through the app_process agent (F116).
///
/// Android 11 and later grant the shell user the permission behind
/// <c>WifiManager.getPrivilegedConfiguredNetworks()</c>, which the agent runs as. That makes PSK
/// export reachable with no root and without Shizuku being installed, which
/// <see cref="WifiSnapshotService"/> cannot do from plain shell commands.
///
/// When the agent is missing or the privileged call does not answer, this returns a failure with a
/// stated reason so the caller can fall back to the SSID-only path rather than appearing to work.
/// </summary>
public sealed class WifiPskExportService
{
    public const string Op = "wifi-psk";

    private readonly AppProcessAgentService _agent;
    private readonly ILogger _log;

    public WifiPskExportService(AppProcessAgentService agent, ILogger log)
    {
        _agent = agent;
        _log = log.ForContext<WifiPskExportService>();
    }

    /// <summary>
    /// Pushes the agent, reads saved networks, and removes the agent again unless
    /// <paramref name="keepAgent"/> is set.
    /// </summary>
    public async Task<WifiPskExportResult> ExportAsync(
        DeviceData device,
        string agentJarPath,
        bool keepAgent = false,
        CancellationToken ct = default)
    {
        if (!File.Exists(agentJarPath))
            return Failure($"agent JAR not found at {agentJarPath}");

        try
        {
            await _agent.PushAgentAsync(device, agentJarPath, ct);
            var raw = await _agent.InvokeAsync(device, $"{{\"op\":\"{Op}\"}}", ct);
            return Parse(raw);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failure(ex.Message);
        }
        finally
        {
            if (!keepAgent)
            {
                try
                {
                    await _agent.RemoveAgentAsync(device, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Could not remove the agent JAR after a Wi-Fi export");
                }
            }
        }
    }

    /// <summary>
    /// Parses the agent's v1 envelope into networks. Separated from the ADB call so the
    /// parsing, including the redaction guarantees, is testable without a device.
    /// </summary>
    public static WifiPskExportResult Parse(string? agentOutput)
    {
        var json = (agentOutput ?? "").Trim();
        if (!HelperProviderContract.TryParseEnvelope(json, out var envelope))
            return Failure("the agent returned an unparseable payload");

        if (!envelope!.IsOk)
            return Failure(envelope.Error?.Message ?? envelope.Status);

        var networks = new List<WifiNetwork>(envelope.Count);
        foreach (var item in envelope.Items.EnumerateArray())
        {
            var ssid = ReadString(item, "ssid");
            if (string.IsNullOrEmpty(ssid)) continue;

            networks.Add(new WifiNetwork
            {
                Ssid = ssid,
                Psk = ReadString(item, "psk") ?? "",
                Hidden = item.TryGetProperty("hidden", out var hidden)
                         && hidden.ValueKind == JsonValueKind.True,
                Auth = ParseAuth(ReadString(item, "auth")),
            });
        }

        return new WifiPskExportResult(networks, Error: null);
    }

    public static WifiAuth ParseAuth(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "nopass" => WifiAuth.Nopass,
        "wep" => WifiAuth.Wep,
        "wpa-eap" or "wpaeap" => WifiAuth.WpaEap,
        _ => WifiAuth.Wpa,
    };

    private static WifiPskExportResult Failure(string reason) =>
        new(Array.Empty<WifiNetwork>(), reason);

    private static string? ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
