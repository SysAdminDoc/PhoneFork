using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public sealed class AdbPairingServiceTests
{
    [Fact]
    public void ParsePairingQr_ReadsAndroidWirelessDebuggingPayload()
    {
        var parsed = AdbPairingService.ParsePairingQr("WIFI:T:ADB;S:192.168.1.15:37123;P:123456;;");

        Assert.NotNull(parsed);
        Assert.Equal("192.168.1.15:37123", parsed.Value.ServiceName);
        Assert.Equal("123456", parsed.Value.Code);
    }

    [Fact]
    public void ParsePairingQr_UnescapesWifiFieldSeparators()
    {
        var parsed = AdbPairingService.ParsePairingQr(@"WIFI:T:ADB;S:host\:37123;P:12\;34;;");

        Assert.NotNull(parsed);
        Assert.Equal("host:37123", parsed.Value.ServiceName);
        Assert.Equal("12;34", parsed.Value.Code);
    }

    [Fact]
    public void ParsePairingQr_RejectsNonWifiPayload()
    {
        Assert.Null(AdbPairingService.ParsePairingQr("not a pairing payload"));
    }
}
