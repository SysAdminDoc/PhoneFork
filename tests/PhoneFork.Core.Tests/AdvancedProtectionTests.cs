using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F114 — Advanced Protection blocks the sideloading permission and disables USB data while the
/// device is locked, so pre-flight must name it. Unknown must never be reported as Off: an
/// unreadable setting is not proof the feature is disabled.
/// </summary>
public class AdvancedProtectionTests
{
    [Theory]
    [InlineData("1", AdvancedProtectionState.On)]
    [InlineData("true", AdvancedProtectionState.On)]
    [InlineData("0", AdvancedProtectionState.Off)]
    [InlineData("false", AdvancedProtectionState.Off)]
    [InlineData(" 1 ", AdvancedProtectionState.On)]
    [InlineData("TRUE", AdvancedProtectionState.On)]
    public void InterpretReadsRecognisedValues(string raw, AdvancedProtectionState expected)
    {
        Assert.Equal(expected, AdvancedProtectionService.Interpret(raw));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("NULL")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("2")]
    [InlineData("Cannot read setting")]
    public void UnreadableOrUnrecognisedValuesAreUnknownNotOff(string? raw)
    {
        // An absent key proves only that the device does not store the flag there.
        Assert.Equal(AdvancedProtectionState.Unknown, AdvancedProtectionService.Interpret(raw));
    }

    [Fact]
    public void ProbeReadsMoreThanOneCandidateKey()
    {
        Assert.True(AdvancedProtectionService.CandidateKeys.Count > 1);
        Assert.Contains("aapm_activated", AdvancedProtectionService.CandidateKeys);
    }

    [Fact]
    public void AnEnabledDeviceIsReportedAsABlocker()
    {
        var finding = new AdvancedProtectionReport(AdvancedProtectionState.On, "aapm_activated=1")
            .ToFinding("destination");

        Assert.Equal(HonestyLevel.Blocker, finding.Level);
        Assert.Contains("destination", finding.Title);
        Assert.Contains("Advanced Protection", finding.Title);
    }

    [Fact]
    public void AnUnknownStateIsAWarningNotAnInfo()
    {
        var finding = new AdvancedProtectionReport(AdvancedProtectionState.Unknown, "aapm_activated=null")
            .ToFinding("source");

        Assert.Equal(HonestyLevel.Warning, finding.Level);
        Assert.Contains("unknown", finding.Title, StringComparison.OrdinalIgnoreCase);
        // The evidence must reach the operator so they can tell "not read" from "read as off".
        Assert.Contains("aapm_activated=null", finding.Detail);
    }

    [Fact]
    public void AnOffDeviceIsInformationalOnly()
    {
        var finding = new AdvancedProtectionReport(AdvancedProtectionState.Off, "aapm_activated=0")
            .ToFinding("source");

        Assert.Equal(HonestyLevel.Info, finding.Level);
    }

    [Fact]
    public void TheBlockerNamesTheOperationsThatStopWorking()
    {
        var detail = new AdvancedProtectionReport(AdvancedProtectionState.On, "aapm_activated=1")
            .ToFinding("destination").Detail;

        Assert.Contains("sideloading", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("USB data", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Developer options", detail, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(AdvancedProtectionReport.AffectedOperations);
    }

    [Fact]
    public void FindingIdsAreDistinctPerRoleSoBothPhonesRender()
    {
        var source = new AdvancedProtectionReport(AdvancedProtectionState.On, "x").ToFinding("source");
        var destination = new AdvancedProtectionReport(AdvancedProtectionState.On, "x").ToFinding("destination");

        Assert.NotEqual(source.Id, destination.Id);
    }
}
