using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public sealed class AndroidUserProfileTests
{
    [Fact]
    public void ParseReportAcceptsPrimaryOwnerOnly()
    {
        var report = AndroidUserProfileService.ParseReport(
            """
            Users:
                UserInfo{0:Owner:13} running
            """,
            "0");

        Assert.True(report.IsReliable);
        var user = Assert.Single(report.Users);
        Assert.Equal(0, user.UserId);
        Assert.True(user.IsCurrent);
        Assert.True(user.IsRunning);
        Assert.Empty(report.PrimaryUserWriteBlockers());
    }

    [Fact]
    public void ManagedProfileBlocksPrimaryUserWrites()
    {
        var report = AndroidUserProfileService.ParseReport(
            """
            Users:
                UserInfo{0:Owner:13} running
                UserInfo{10:Work profile:30} running
            """,
            "0");

        Assert.Contains(report.Users, u => u.UserId == 10 && u.IsManagedProfile);
        Assert.Contains(report.PrimaryUserWriteBlockers(), b => b.Contains("Managed/work profile", StringComparison.Ordinal));
    }

    [Fact]
    public void SecondaryCurrentUserBlocksPrimaryUserWrites()
    {
        var report = AndroidUserProfileService.ParseReport(
            """
            Users:
                UserInfo{0:Owner:13} running
                UserInfo{11:Guest:14} running
            """,
            "11");

        Assert.Contains(report.Users, u => u.UserId == 11 && u.IsGuest && u.IsCurrent);
        Assert.Contains(report.PrimaryUserWriteBlockers(), b => b.Contains("Current Android user is 11", StringComparison.Ordinal));
        Assert.Contains(report.PrimaryUserWriteBlockers(), b => b.Contains("Additional Android user", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyUserListIsNotReliableAndBlocksWrites()
    {
        var report = AndroidUserProfileService.ParseReport("", "0");

        Assert.False(report.IsReliable);
        Assert.Contains(report.PrimaryUserWriteBlockers(), b => b.Contains("could not be verified", StringComparison.Ordinal));
    }

    [Fact]
    public void UnparseableCurrentUserBlocksWrites()
    {
        var report = AndroidUserProfileService.ParseReport(
            """
            Users:
                UserInfo{0:Owner:13} running
            """,
            "Error: command unavailable");

        Assert.False(report.IsReliable);
        Assert.Contains(report.PrimaryUserWriteBlockers(), b => b.Contains("current user id could not be parsed", StringComparison.Ordinal));
    }
}
