using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class MdnsParseTests
{
    [Fact]
    public void EmptyOutputIsEmpty()
    {
        Assert.Empty(AdbPairingService.ParseMdnsServices(""));
        Assert.Empty(AdbPairingService.ParseMdnsServices("   \n  \n"));
    }

    [Fact]
    public void IgnoresBannerLine()
    {
        var output = "List of discovered mdns services\n" +
                     "adb-R5CY-AbCd\t_adb-tls-connect._tcp\t192.168.1.10:39123\n";
        var rows = AdbPairingService.ParseMdnsServices(output);
        Assert.Single(rows);
        Assert.Equal("adb-R5CY-AbCd", rows[0].Instance);
        Assert.Equal("_adb-tls-connect._tcp", rows[0].ServiceType);
        Assert.Equal("192.168.1.10:39123", rows[0].HostPort);
        Assert.True(rows[0].IsConnect);
        Assert.False(rows[0].IsPairing);
    }

    [Fact]
    public void HandlesPairingService()
    {
        var output =
            "adb-R5CY-AbCd\t_adb-tls-pairing._tcp\t192.168.1.10:45678\n" +
            "adb-RFCY-WxYz\t_adb-tls-connect._tcp\t192.168.1.11:39124\n";
        var rows = AdbPairingService.ParseMdnsServices(output);
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].IsPairing);
        Assert.True(rows[1].IsConnect);
    }

    [Fact]
    public void SkipsRowsWithFewerThanThreeFields()
    {
        var output = "broken-row-without-tabs\nfoo\tbar\n";
        Assert.Empty(AdbPairingService.ParseMdnsServices(output));
    }
}
