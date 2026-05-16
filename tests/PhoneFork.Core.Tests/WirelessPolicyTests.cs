using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.Core.Tests;

public class WirelessPolicyTests
{
    private static ILogger NullLog() => new LoggerConfiguration().CreateLogger();

    [Fact]
    public void UsbTransportAlwaysAllowed()
    {
        var policy = new WirelessPolicy(NullLog());
        // USB is allowed even without an opt-in (USB is the default trust posture).
        var posture = new SecurityPosture("R5CY34G070L", AdbTransport.Usb, new DateOnly(2026, 4, 1),
            PatchLevelStatus.BelowCveFix, false);
        var decision = policy.Evaluate(posture);
        Assert.Equal(WirelessDecision.Allowed, decision.Decision);
    }

    [Fact]
    public void WirelessBlockedWithoutOptIn()
    {
        var policy = new WirelessPolicy(NullLog());
        var posture = new SecurityPosture("192.168.1.10:39123", AdbTransport.Tcp, new DateOnly(2026, 5, 1),
            PatchLevelStatus.MeetsCveFix, false);
        var decision = policy.Evaluate(posture);
        Assert.Equal(WirelessDecision.BlockedByPolicy, decision.Decision);
    }

    [Fact]
    public void WirelessBlockedOnUnpatchedDevice()
    {
        var policy = new WirelessPolicy(NullLog());
        policy.OptInWireless();
        var posture = new SecurityPosture("192.168.1.10:39123", AdbTransport.Tcp, new DateOnly(2026, 4, 1),
            PatchLevelStatus.BelowCveFix, true);
        var decision = policy.Evaluate(posture);
        Assert.Equal(WirelessDecision.BlockedByPatchLevel, decision.Decision);
        Assert.Contains("CVE-2026-0073", decision.Reason);
    }

    [Fact]
    public void UnknownPatchLevelBlockedByDefault()
    {
        var policy = new WirelessPolicy(NullLog());
        policy.OptInWireless();
        var posture = new SecurityPosture("192.168.1.10:39123", AdbTransport.Tcp, null,
            PatchLevelStatus.Unknown, false);
        var decision = policy.Evaluate(posture);
        Assert.Equal(WirelessDecision.BlockedByPatchLevel, decision.Decision);
    }

    [Fact]
    public void UnpatchedOverrideAllowsUnpatchedWireless()
    {
        var policy = new WirelessPolicy(NullLog());
        policy.OptInWireless();
        policy.AllowUnpatchedOverride = true;
        var posture = new SecurityPosture("192.168.1.10:39123", AdbTransport.Tcp, new DateOnly(2026, 4, 1),
            PatchLevelStatus.BelowCveFix, true);
        var decision = policy.Evaluate(posture);
        Assert.Equal(WirelessDecision.Allowed, decision.Decision);
    }

    [Fact]
    public void PatchedWirelessAllowedAfterOptIn()
    {
        var policy = new WirelessPolicy(NullLog());
        policy.OptInWireless();
        var posture = new SecurityPosture("192.168.1.10:39123", AdbTransport.Tcp, new DateOnly(2026, 5, 1),
            PatchLevelStatus.MeetsCveFix, false);
        var decision = policy.Evaluate(posture);
        Assert.Equal(WirelessDecision.Allowed, decision.Decision);
    }

    [Fact]
    public void SessionTimeoutClosesAccess()
    {
        var policy = new WirelessPolicy(NullLog());
        policy.OptInWireless(TimeSpan.FromMilliseconds(1));
        Thread.Sleep(20);
        Assert.True(policy.IsSessionExpired());
        var posture = new SecurityPosture("192.168.1.10:39123", AdbTransport.Tcp, new DateOnly(2026, 5, 1),
            PatchLevelStatus.MeetsCveFix, false);
        var decision = policy.Evaluate(posture);
        Assert.Equal(WirelessDecision.BlockedByPolicy, decision.Decision);
        Assert.Contains("expired", decision.Reason);
    }

    [Fact]
    public void KillSwitchTerminatesSession()
    {
        var policy = new WirelessPolicy(NullLog());
        policy.OptInWireless();
        Assert.True(policy.WirelessOptedIn);
        policy.KillWireless();
        Assert.False(policy.WirelessOptedIn);
    }

    [Theory]
    [InlineData("R5CY34G070L", AdbTransport.Usb)]
    [InlineData("192.168.1.10:39123", AdbTransport.Tcp)]
    [InlineData("emulator-5554", AdbTransport.Usb)]
    [InlineData("", AdbTransport.Unknown)]
    [InlineData("host:notaport", AdbTransport.Usb)]
    public void ClassifyTransport(string serial, AdbTransport expected)
    {
        Assert.Equal(expected, SecurityPostureService.ClassifyTransport(serial));
    }

    [Theory]
    [InlineData("2026-05-01", PatchLevelStatus.MeetsCveFix)]
    [InlineData("2026-06-15", PatchLevelStatus.MeetsCveFix)]
    [InlineData("2026-04-30", PatchLevelStatus.BelowCveFix)]
    [InlineData("2024-12-01", PatchLevelStatus.BelowCveFix)]
    [InlineData("", PatchLevelStatus.Unknown)]
    [InlineData("not a date", PatchLevelStatus.Unknown)]
    public void ClassifyPatch(string raw, PatchLevelStatus expected)
    {
        var parsed = SecurityPostureService.ParsePatchDate(raw);
        Assert.Equal(expected, SecurityPostureService.ClassifyPatch(parsed));
    }

    [Theory]
    [InlineData("2026-05-01-1", 2026, 5, 1)]
    [InlineData("2026-05-01", 2026, 5, 1)]
    public void ParsePatchDateHandlesSamsungSuffix(string raw, int y, int m, int d)
    {
        var parsed = SecurityPostureService.ParsePatchDate(raw);
        Assert.Equal(new DateOnly(y, m, d), parsed);
    }
}
