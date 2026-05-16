using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class SmartSwitchDetectionTests
{
    [Fact]
    public void ProbeReturnsAnEnumeratedResultOnAnyHost()
    {
        // The probe always returns; non-Windows hosts get NotInstalled, Windows hosts
        // get whatever the registry / store package directory looks like in CI.
        // We can't assert specific install states because that would couple the test
        // to host configuration, but we can assert the shape of the return value.
        var result = SmartSwitchDetection.Probe();
        Assert.NotNull(result);
        Assert.True(Enum.IsDefined(result.Install));
        Assert.Equal(result.Install != SmartSwitchInstall.NotInstalled, result.IsAvailable);
    }

    [Fact]
    public void ResultRecordIsValueEquality()
    {
        var a = new SmartSwitchDetectionResult(SmartSwitchInstall.LegacyMsi, "C:\\Smart Switch", null, "C:\\Users\\Me\\Documents\\Samsung\\SmartSwitch");
        var b = new SmartSwitchDetectionResult(SmartSwitchInstall.LegacyMsi, "C:\\Smart Switch", null, "C:\\Users\\Me\\Documents\\Samsung\\SmartSwitch");
        Assert.Equal(a, b);
    }
}
