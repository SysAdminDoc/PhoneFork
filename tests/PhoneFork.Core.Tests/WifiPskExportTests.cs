using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F116 — saved Wi-Fi keys come back through the app_process agent. The parse must be faithful,
/// the failure path must state a reason so the caller can degrade to SSID-only, and nothing that
/// carries a key may reach a log line or a receipt.
/// </summary>
public class WifiPskExportTests
{
    private static string Envelope(string itemsJson, int count) =>
        $$"""
        {"schema":"phonefork.helper.v1","authority":"agent","status":"ok","mode":"export","count":{{count}},"items":{{itemsJson}},"capabilities":{},"warnings":[]}
        """;

    [Fact]
    public void ParsesSsidAuthHiddenAndKey()
    {
        var result = WifiPskExportService.Parse(Envelope(
            """[{"ssid":"HomeNet","hidden":false,"auth":"wpa","psk":"correcthorse"}]""", 1));

        Assert.True(result.Success);
        var net = Assert.Single(result.Networks);
        Assert.Equal("HomeNet", net.Ssid);
        Assert.Equal(WifiAuth.Wpa, net.Auth);
        Assert.False(net.Hidden);
        Assert.Equal("correcthorse", net.Psk);
    }

    [Theory]
    [InlineData("nopass", WifiAuth.Nopass)]
    [InlineData("wep", WifiAuth.Wep)]
    [InlineData("wpa", WifiAuth.Wpa)]
    [InlineData("wpa-eap", WifiAuth.WpaEap)]
    [InlineData("WPA-EAP", WifiAuth.WpaEap)]
    [InlineData("something-new", WifiAuth.Wpa)]
    [InlineData(null, WifiAuth.Wpa)]
    public void MapsEveryAuthTheAgentCanEmit(string? raw, WifiAuth expected)
    {
        Assert.Equal(expected, WifiPskExportService.ParseAuth(raw));
    }

    [Fact]
    public void AnOpenNetworkHasNoKeyAndIsStillReturned()
    {
        var result = WifiPskExportService.Parse(Envelope(
            """[{"ssid":"CoffeeShop","hidden":false,"auth":"nopass"}]""", 1));

        var net = Assert.Single(result.Networks);
        Assert.Equal("", net.Psk);
        Assert.Equal(0, result.WithPskCount);
    }

    [Fact]
    public void HiddenNetworksAreFlagged()
    {
        var result = WifiPskExportService.Parse(Envelope(
            """[{"ssid":"Hidden","hidden":true,"auth":"wpa","psk":"k"}]""", 1));

        Assert.True(Assert.Single(result.Networks).Hidden);
    }

    [Fact]
    public void RowsWithoutAnSsidAreSkipped()
    {
        var result = WifiPskExportService.Parse(Envelope(
            """[{"ssid":"","auth":"wpa"},{"auth":"wpa"},{"ssid":"Real","auth":"wpa","psk":"k"}]""", 3));

        Assert.Equal("Real", Assert.Single(result.Networks).Ssid);
    }

    [Fact]
    public void AnAgentErrorBecomesAStatedReasonNotAnEmptySuccess()
    {
        // The caller must be able to tell "no networks" from "could not read", because the
        // first is a real answer and the second means fall back to SSID-only.
        var errorEnvelope =
            """
            {"schema":"phonefork.helper.v1","authority":"agent","status":"error","mode":"export","count":0,"items":[],"error":{"code":"privileged-networks-unavailable","message":"getPrivilegedConfiguredNetworks() did not answer"}}
            """;

        var result = WifiPskExportService.Parse(errorEnvelope);

        Assert.False(result.Success);
        Assert.Empty(result.Networks);
        Assert.Contains("getPrivilegedConfiguredNetworks", result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not json at all")]
    [InlineData("""{"schema":"phonefork.helper.v1","authority":"banking","status":"ok","items":[]}""")]
    public void UnparseableOutputFailsWithAReason(string? output)
    {
        var result = WifiPskExportService.Parse(output);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void AnEmptyButSuccessfulExportIsDistinctFromAFailure()
    {
        var result = WifiPskExportService.Parse(Envelope("[]", 0));

        Assert.True(result.Success);
        Assert.Empty(result.Networks);
        Assert.Null(result.Error);
    }

    [Fact]
    public void TheSummaryNeverContainsAKey()
    {
        // Summary is the only field callers put in logs, receipts and the status bar.
        var result = WifiPskExportService.Parse(Envelope(
            """[{"ssid":"HomeNet","auth":"wpa","psk":"correcthorsebatterystaple"},{"ssid":"Other","auth":"wpa","psk":"hunter2"}]""", 2));

        Assert.DoesNotContain("correcthorsebatterystaple", result.Summary);
        Assert.DoesNotContain("hunter2", result.Summary);
        Assert.Contains("2 saved network(s)", result.Summary);
        Assert.Contains("2 with a recoverable key", result.Summary);
    }

    [Fact]
    public void TheFailureSummaryStatesTheReason()
    {
        var result = WifiPskExportService.Parse("garbage");

        Assert.Contains("unavailable", result.Summary);
    }
}
