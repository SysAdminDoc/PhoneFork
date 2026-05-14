using PhoneFork.Core.Models;
using QRCoder;

namespace PhoneFork.Core.Services;

/// <summary>
/// Builds standard WIFI: URI payloads and renders them as scannable QR codes. Used by PhoneFork's
/// QR-bridge fallback path — the destination scans the QR with the Wi-Fi-join camera affordance
/// and connects without any on-device install or root.
/// </summary>
public static class WifiQrService
{
    /// <summary>
    /// Build the WIFI: URI per the WPA3 schema:
    ///   WIFI:T:&lt;auth&gt;;S:&lt;ssid&gt;;P:&lt;psk&gt;;H:&lt;hidden&gt;;;
    /// </summary>
    public static string BuildPayload(WifiNetwork n)
    {
        var gen = new PayloadGenerator.WiFi(
            ssid: n.Ssid,
            password: n.Psk,
            authenticationMode: n.Auth switch
            {
                WifiAuth.Wpa     => PayloadGenerator.WiFi.Authentication.WPA,
                WifiAuth.Wep     => PayloadGenerator.WiFi.Authentication.WEP,
                WifiAuth.Nopass  => PayloadGenerator.WiFi.Authentication.nopass,
                WifiAuth.WpaEap  => PayloadGenerator.WiFi.Authentication.WPA,
                _ => PayloadGenerator.WiFi.Authentication.WPA,
            },
            isHiddenSSID: n.Hidden);
        return gen.ToString();
    }

    /// <summary>
    /// Render the QR as a transparent-background PNG at the requested pixels-per-module.
    /// Returns the file path written.
    /// </summary>
    public static string RenderPng(WifiNetwork n, string outPath, int pixelsPerModule = 8)
    {
        var payload = BuildPayload(n);
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var pngQr = new PngByteQRCode(qrData);
        var bytes = pngQr.GetGraphic(pixelsPerModule);
        File.WriteAllBytes(outPath, bytes);
        return outPath;
    }

    /// <summary>
    /// Render the QR as SVG. SVG scales without aliasing.
    /// </summary>
    public static string RenderSvg(WifiNetwork n, string outPath, int pixelsPerModule = 8)
    {
        var payload = BuildPayload(n);
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var svgQr = new SvgQRCode(qrData);
        var svg = svgQr.GetGraphic(pixelsPerModule);
        File.WriteAllText(outPath, svg);
        return outPath;
    }
}
