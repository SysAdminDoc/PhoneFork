using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.Core.Tests;

public class CscPostureTests
{
    [Fact]
    public void CountryMismatchIsDetected()
    {
        var posture = new CscPosture("XSA", "XAR", "US", "GB", "en-US", "en-GB");
        Assert.True(posture.CountryMismatch);
        Assert.True(posture.LocaleMismatch);
    }

    [Fact]
    public void SameCsCDoesNotFlag()
    {
        var posture = new CscPosture("XSA", "XSA", "US", "US", "en-US", "en-US");
        Assert.False(posture.CountryMismatch);
        Assert.False(posture.LocaleMismatch);
    }
}

public class MessageTransitionTests
{
    [Fact]
    public void UsSamsungMessagesDefaultBlocksHelperSmsUntilGoogleTransition()
    {
        var report = MessageTransitionService.Assess(
            MessageTransitionService.SamsungMessagesPackage,
            samsungMessagesInstalled: true,
            googleMessagesInstalled: true,
            sourceCountry: "US");

        Assert.False(report.CanUseHelperSms);
        Assert.True(report.IsUsMarket);
        Assert.Contains(report.Findings, f => f.Id == "samsung-messages-us-transition"
                                             && f.Level == HonestyLevel.Warning
                                             && f.Detail.Contains("July 2026")
                                             && f.Detail.Contains("24 hours"));
    }

    [Fact]
    public void GoogleMessagesDefaultClearsHelperSmsGate()
    {
        var report = MessageTransitionService.Assess(
            MessageTransitionService.GoogleMessagesPackage,
            samsungMessagesInstalled: true,
            googleMessagesInstalled: true,
            sourceCountry: "US");

        Assert.True(report.CanUseHelperSms);
        Assert.Contains(report.Findings, f => f.Id == "sms-default-google");
    }

    [Fact]
    public void MissingDefaultSmsRoleKeepsHelperSmsGateClosed()
    {
        var report = MessageTransitionService.Assess(
            defaultSmsPackage: null,
            samsungMessagesInstalled: false,
            googleMessagesInstalled: false,
            sourceCountry: "CA");

        Assert.False(report.CanUseHelperSms);
        Assert.Contains(report.Findings, f => f.Id == "sms-default-missing"
                                             && f.Level == HonestyLevel.Warning);
    }
}

public class IntegrityModeTests
{
    [Theory]
    [InlineData("Hello, World!", 0xec4ac3d0u)]
    [InlineData("", 0u)]
    [InlineData("a", 0xe8b7be43u)]
    public void Crc32ImplementationMatchesPolynomial(string text, uint expected)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var actual = MediaIntegrityService.Crc32(bytes);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IntegrityReportIsCleanWhenEverythingMatches()
    {
        var report = new IntegrityReport(MediaIntegrityMode.SizeAndMtime, 10, 10,
            Array.Empty<IntegrityMismatch>(), Array.Empty<string>());
        Assert.True(report.IsClean);
    }

    [Fact]
    public void IntegrityReportIsNotCleanWhenAnyMismatch()
    {
        var report = new IntegrityReport(MediaIntegrityMode.SizeAndMtime, 10, 9,
            new[] { new IntegrityMismatch("DCIM/a.jpg", "size=10", "size=20", 10) },
            Array.Empty<string>());
        Assert.False(report.IsClean);
    }

    [Fact]
    public void IntegrityReportFlagsMissingDestinationFiles()
    {
        var report = new IntegrityReport(MediaIntegrityMode.SizeAndMtime, 10, 9,
            Array.Empty<IntegrityMismatch>(),
            new[] { "DCIM/missing.jpg" });
        Assert.False(report.IsClean);
    }
}

public class BurstModeTests
{
    private static ILogger NullLog() => new LoggerConfiguration().CreateLogger();

    [Fact]
    public void EnableThenDisableTogglesEnvVar()
    {
        var prior = Environment.GetEnvironmentVariable("ADB_BURST_MODE");
        try
        {
            Environment.SetEnvironmentVariable("ADB_BURST_MODE", null);
            var svc = new AdbBurstModeService(NullLog());
            Assert.False(svc.IsBurstEnabled);
            Assert.True(svc.Enable());
            Assert.True(svc.IsBurstEnabled);
            Assert.False(svc.Enable()); // already on -> no restart required
            Assert.True(svc.Disable());
            Assert.False(svc.IsBurstEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ADB_BURST_MODE", prior);
        }
    }

    [Fact]
    public void SetMapsToEnableDisable()
    {
        var prior = Environment.GetEnvironmentVariable("ADB_BURST_MODE");
        try
        {
            Environment.SetEnvironmentVariable("ADB_BURST_MODE", null);
            var svc = new AdbBurstModeService(NullLog());
            svc.Set(enabled: true);
            Assert.True(svc.IsBurstEnabled);
            svc.Set(enabled: false);
            Assert.False(svc.IsBurstEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ADB_BURST_MODE", prior);
        }
    }
}

public class TrustedRegistryHashRemovalTests : IDisposable
{
    private readonly string _path;

    public TrustedRegistryHashRemovalTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"phonefork-trust-removal-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
    }

    [Fact]
    public void ForgetByHashRemovesEntry()
    {
        var reg = new TrustedPairRegistry(_path, new LoggerConfiguration().CreateLogger());
        var entry = reg.Touch("R5CY", "Phone A", AdbTransport.Usb);
        Assert.True(reg.IsTrusted("R5CY"));

        Assert.True(reg.ForgetByHash(entry.SerialHashValue));
        Assert.False(reg.IsTrusted("R5CY"));
        Assert.False(reg.ForgetByHash(entry.SerialHashValue));
    }
}
