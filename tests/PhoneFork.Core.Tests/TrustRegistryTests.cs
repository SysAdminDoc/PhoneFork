using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.Core.Tests;

public class SerialHashTests
{
    [Fact]
    public void EmptyOrNullReturnsEmpty()
    {
        Assert.Equal("", SerialHash.Of(""));
        Assert.Equal("", SerialHash.Of(null));
        Assert.Equal("", SerialHash.Of("   "));
    }

    [Fact]
    public void Deterministic()
    {
        Assert.Equal(SerialHash.Of("R5CY34G070L"), SerialHash.Of("R5CY34G070L"));
    }

    [Fact]
    public void DifferentSerialsHashDifferently()
    {
        Assert.NotEqual(SerialHash.Of("R5CY34G070L"), SerialHash.Of("RFCY34G070A"));
    }

    [Fact]
    public void TwelveHexCharsLowercase()
    {
        var h = SerialHash.Of("R5CY34G070L");
        Assert.Equal(12, h.Length);
        Assert.Matches("^[0-9a-f]{12}$", h);
    }

    [Fact]
    public void TrimsWhitespace()
    {
        Assert.Equal(SerialHash.Of("R5CY"), SerialHash.Of("  R5CY  "));
    }
}

public class TrustedPairRegistryTests : IDisposable
{
    private readonly string _path;

    public TrustedPairRegistryTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"phonefork-tests-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }

    private static ILogger NullLog() => new LoggerConfiguration().CreateLogger();

    [Fact]
    public void NewRegistryIsEmpty()
    {
        var reg = new TrustedPairRegistry(_path, NullLog());
        Assert.Empty(reg.All);
        Assert.False(reg.IsTrusted("R5CY34G070L"));
    }

    [Fact]
    public void TouchAddsAndPersists()
    {
        var reg = new TrustedPairRegistry(_path, NullLog());
        reg.Touch("R5CY34G070L", "Galaxy S25 Ultra", AdbTransport.Usb);
        Assert.True(reg.IsTrusted("R5CY34G070L"));
        var entry = reg.Get("R5CY34G070L");
        Assert.NotNull(entry);
        Assert.Equal("Galaxy S25 Ultra", entry!.Label);

        // Reload from disk to verify persistence.
        var reloaded = new TrustedPairRegistry(_path, NullLog());
        Assert.True(reloaded.IsTrusted("R5CY34G070L"));
        Assert.Equal(AdbTransport.Usb, reloaded.Get("R5CY34G070L")!.Transport);
    }

    [Fact]
    public void RegistryNeverStoresRawSerial()
    {
        var reg = new TrustedPairRegistry(_path, NullLog());
        reg.Touch("R5CY34G070L", "Galaxy S25 Ultra", AdbTransport.Usb);
        var disk = File.ReadAllText(_path);
        Assert.DoesNotContain("R5CY34G070L", disk);
        Assert.Contains(SerialHash.Of("R5CY34G070L"), disk);
    }

    [Fact]
    public void TouchUpdatesLastSeen()
    {
        var reg = new TrustedPairRegistry(_path, NullLog());
        var first = reg.Touch("R5CY", "Phone A", AdbTransport.Usb);
        Thread.Sleep(10);
        var second = reg.Touch("R5CY", "Phone A", AdbTransport.Usb);
        Assert.True(second.LastSeen >= first.LastSeen);
        Assert.Equal(first.FirstSeen, second.FirstSeen);
    }

    [Fact]
    public void ForgetRemoves()
    {
        var reg = new TrustedPairRegistry(_path, NullLog());
        reg.Touch("R5CY", "Phone A", AdbTransport.Usb);
        Assert.True(reg.Forget("R5CY"));
        Assert.False(reg.IsTrusted("R5CY"));
        Assert.False(reg.Forget("R5CY")); // second remove is a no-op
    }

    [Fact]
    public void TransportUnknownDoesNotOverwriteKnown()
    {
        var reg = new TrustedPairRegistry(_path, NullLog());
        reg.Touch("R5CY", "Phone A", AdbTransport.Tcp, "192.168.1.10:39123");
        var refreshed = reg.Touch("R5CY", "", AdbTransport.Unknown);
        Assert.Equal(AdbTransport.Tcp, refreshed.Transport);
        Assert.Equal("Phone A", refreshed.Label);
    }
}
