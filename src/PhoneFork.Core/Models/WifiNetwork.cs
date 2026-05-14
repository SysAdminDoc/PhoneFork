namespace PhoneFork.Core.Models;

/// <summary>
/// Wi-Fi security type as accepted by the WPA3 WIFI: URI schema.
/// </summary>
public enum WifiAuth
{
    /// <summary>Open / no encryption.</summary>
    Nopass,
    /// <summary>WEP (deprecated, but the QR schema still emits it).</summary>
    Wep,
    /// <summary>WPA/WPA2 personal — also covers WPA3-transition mode.</summary>
    Wpa,
    /// <summary>WPA2-EAP / 802.1x (manual setup required on destination — QR is best-effort).</summary>
    WpaEap,
}

/// <summary>
/// One Wi-Fi network record. PSK may be empty when read from a privileged source
/// (no-root <c>dumpsys wifi</c> redacts the key) — the QR fallback path requires the user to
/// supply it manually.
/// </summary>
public sealed record WifiNetwork
{
    public required string Ssid { get; init; }
    public WifiAuth Auth { get; init; } = WifiAuth.Wpa;
    public string Psk { get; init; } = "";
    public bool Hidden { get; init; }
    /// <summary>If the SSID was read from a device snapshot, the device's serial; otherwise null.</summary>
    public string? SourceSerial { get; init; }
}

/// <summary>
/// Device-region snapshot — used to surface CSC / locale / sales-code mismatches before migration,
/// which is the root cause of many Samsung Wallet / Pass / regional-app restore failures.
/// </summary>
public sealed record CscSnapshot
{
    public required string DeviceSerial { get; init; }
    public string SalesCode { get; init; } = "";
    public string CountryCode { get; init; } = "";
    public string Locale { get; init; } = "";
    public string Timezone { get; init; } = "";
    public string CarrierIso { get; init; } = "";
}
