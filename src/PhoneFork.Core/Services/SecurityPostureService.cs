using System.Globalization;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Captures per-device security/trust posture: ADB transport (USB vs TCP) and Android
/// security patch level. Powers the CVE-2026-0073 wireless gate (F001/F003/F105).
/// </summary>
public sealed class SecurityPostureService
{
    private readonly AdbHostService _host;
    private readonly ILogger _log;

    public SecurityPostureService(AdbHostService host, ILogger log)
    {
        _host = host;
        _log = log.ForContext<SecurityPostureService>();
    }

    /// <summary>
    /// Classify the transport for an ADB serial. Wireless ADB serials are encoded as
    /// <c>host:port</c> by adbd's mdns/connect handshake; USB serials are device IDs.
    /// We also try the live <c>DeviceData.TransportId</c> when available.
    /// </summary>
    public static AdbTransport ClassifyTransport(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return AdbTransport.Unknown;

        // adbd encodes wireless endpoints as host:port. The :port suffix is the canonical signal.
        var colon = serial.IndexOf(':');
        if (colon > 0
            && colon < serial.Length - 1
            && int.TryParse(serial.AsSpan(colon + 1), out var port)
            && port is > 0 and <= 65535)
        {
            return AdbTransport.Tcp;
        }

        return AdbTransport.Usb;
    }

    /// <summary>
    /// Parse Android security-patch property values (e.g. "2026-05-01") to a <see cref="DateOnly"/>.
    /// Returns null on unparseable or empty input.
    /// </summary>
    public static DateOnly? ParsePatchDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;

        // Some Samsung devices report patch as "2026-05-01-1" (security_patch + Knox revision).
        // Drop trailing "-N" suffixes and retry.
        var dash = trimmed.LastIndexOf('-');
        if (dash > 0 && dash >= 7)
        {
            var head = trimmed[..dash];
            if (DateOnly.TryParseExact(head, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
                return d2;
        }

        return null;
    }

    /// <summary>Map a parsed patch date to CVE-2026-0073 status.</summary>
    public static PatchLevelStatus ClassifyPatch(DateOnly? patchDate) => patchDate is null
        ? PatchLevelStatus.Unknown
        : patchDate.Value >= SecurityPosture.CveFixPatchLevel
            ? PatchLevelStatus.MeetsCveFix
            : PatchLevelStatus.BelowCveFix;

    /// <summary>
    /// Read <c>ro.build.version.security_patch</c> from the device and combine with the transport
    /// classification to build a posture record. Falls back to an Unknown patch level if shell
    /// access is denied or the property is empty.
    /// </summary>
    public SecurityPosture Probe(DeviceData device)
    {
        var serial = device.Serial ?? "";
        var transport = ClassifyTransport(serial);

        DateOnly? patch = null;
        try
        {
            var raw = _host.Client.Shell(device, "getprop ro.build.version.security_patch").Trim();
            patch = ParsePatchDate(raw);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "security_patch probe failed for {Serial}", serial);
        }

        var status = ClassifyPatch(patch);
        var vuln = transport == AdbTransport.Tcp && status == PatchLevelStatus.BelowCveFix;
        return new SecurityPosture(serial, transport, patch, status, vuln);
    }
}
