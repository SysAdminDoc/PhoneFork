using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F111 — the helper ships no launcher activity, so it can never raise a runtime permission
/// prompt. The host must grant the dangerous permissions itself, and the probe must read back
/// real state rather than assuming the grant worked.
/// </summary>
public class HelperPermissionTests
{
    [Fact]
    public void RuntimePermissionsCoverEveryProviderThatNeedsADangerousGrant()
    {
        Assert.Contains("android.permission.READ_SMS", HelperAppService.RuntimePermissions);
        Assert.Contains("android.permission.READ_CALL_LOG", HelperAppService.RuntimePermissions);
        Assert.Contains("android.permission.READ_CONTACTS", HelperAppService.RuntimePermissions);
    }

    [Fact]
    public void PrivilegedPermissionsAreNeverAttemptedAsRuntimeGrants()
    {
        // pm grant cannot satisfy a signature/privileged permission; attempting one just
        // produces a confusing failure line in the log.
        Assert.Empty(HelperAppService.RuntimePermissions.Intersect(HelperAppService.PrivilegedPermissions));
        Assert.Contains("android.permission.WRITE_SMS", HelperAppService.PrivilegedPermissions);
        Assert.Contains("android.permission.READ_USER_DICTIONARY", HelperAppService.PrivilegedPermissions);
    }

    [Fact]
    public void EveryGrantablePermissionIsDeclaredInTheHelperManifest()
    {
        var manifest = File.ReadAllText(RepoFile("helper-apk/app/src/main/AndroidManifest.xml"));
        foreach (var permission in HelperAppService.RuntimePermissions)
            Assert.Contains($"android:name=\"{permission}\"", manifest);
    }

    [Fact]
    public void ParsePermissionDumpReadsGrantedTrue()
    {
        var report = HelperAppService.ParsePermissionDump(SampleDump(
            ("android.permission.READ_SMS", true),
            ("android.permission.READ_CALL_LOG", true),
            ("android.permission.WRITE_CALL_LOG", true),
            ("android.permission.READ_CONTACTS", true),
            ("android.permission.WRITE_CONTACTS", true)));

        Assert.True(report.AllGranted);
        Assert.True(report.CanReadPrivilegedCategories);
        Assert.Equal(HelperAppService.RuntimePermissions.Count, report.Granted.Count);
    }

    [Fact]
    public void ParsePermissionDumpReadsGrantedFalse()
    {
        var report = HelperAppService.ParsePermissionDump(SampleDump(
            ("android.permission.READ_SMS", false),
            ("android.permission.READ_CALL_LOG", true),
            ("android.permission.WRITE_CALL_LOG", true),
            ("android.permission.READ_CONTACTS", true),
            ("android.permission.WRITE_CONTACTS", true)));

        Assert.False(report.AllGranted);
        Assert.False(report.CanReadPrivilegedCategories);
        Assert.Equal("granted=false", report.Failed["android.permission.READ_SMS"]);
    }

    [Fact]
    public void ParsePermissionDumpTreatsAnAbsentPermissionAsNotGranted()
    {
        // This is the pre-F111 state: `pm install -r` with no -g leaves the runtime block empty.
        var report = HelperAppService.ParsePermissionDump("Packages:\n  Package [com.sysadmindoc.phonefork.helper]\n");

        Assert.False(report.AllGranted);
        Assert.False(report.CanReadPrivilegedCategories);
        Assert.Empty(report.Granted);
        Assert.All(HelperAppService.RuntimePermissions, p => Assert.Equal("not-listed", report.Failed[p]));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ParsePermissionDumpHandlesEmptyOutput(string? dump)
    {
        var report = HelperAppService.ParsePermissionDump(dump);

        Assert.Empty(report.Granted);
        Assert.False(report.CanReadPrivilegedCategories);
    }

    [Fact]
    public void ParsePermissionDumpDoesNotConfuseASimilarlyNamedPermission()
    {
        // READ_CALL_LOG must not be satisfied by a WRITE_CALL_LOG line, and a granted=true
        // elsewhere in the dump must not leak onto a denied permission's line.
        var dump =
            "    runtime permissions:\n" +
            "      android.permission.WRITE_CALL_LOG: granted=true\n" +
            "      android.permission.READ_CALL_LOG: granted=false\n";

        var report = HelperAppService.ParsePermissionDump(dump);

        Assert.Contains("android.permission.WRITE_CALL_LOG", report.Granted);
        Assert.DoesNotContain("android.permission.READ_CALL_LOG", report.Granted);
        Assert.Equal("granted=false", report.Failed["android.permission.READ_CALL_LOG"]);
    }

    private static string SampleDump(params (string Permission, bool Granted)[] entries)
    {
        var lines = entries.Select(e =>
            $"      {e.Permission}: granted={e.Granted.ToString().ToLowerInvariant()}, flags=[ USER_SET ]");
        return "    runtime permissions:\n" + string.Join('\n', lines) + '\n';
    }

    private static string RepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PhoneFork.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}

/// <summary>
/// Multi-user dumps carry one runtime-permission block per Android user. PhoneFork targets
/// user 0, which dumpsys emits first, so the parser must not read a later user's grants.
/// </summary>
public class HelperPermissionMultiUserTests
{
    private const string MultiUserDump = """
        Packages:
          Package [com.sysadmindoc.phonefork.helper]
            User 0:
              runtime permissions:
                android.permission.READ_SMS: granted=true
                android.permission.READ_CALL_LOG: granted=true
                android.permission.WRITE_CALL_LOG: granted=true
                android.permission.READ_CONTACTS: granted=true
                android.permission.WRITE_CONTACTS: granted=true
            User 10:
              runtime permissions:
                android.permission.READ_SMS: granted=false
                android.permission.READ_CALL_LOG: granted=false
                android.permission.WRITE_CALL_LOG: granted=false
                android.permission.READ_CONTACTS: granted=false
                android.permission.WRITE_CONTACTS: granted=false
        """;

    [Fact]
    public void ReadsUserZeroNotASecondaryUser()
    {
        var report = HelperAppService.ParsePermissionDump(MultiUserDump);

        Assert.True(report.AllGranted);
        Assert.True(report.CanReadPrivilegedCategories);
    }

    [Fact]
    public void ASecondaryUserGrantDoesNotMaskAUserZeroDenial()
    {
        // Same dump with the two users' states swapped: user 0 denied, user 10 granted.
        var swapped = MultiUserDump
            .Replace("granted=true", "TEMP", StringComparison.Ordinal)
            .Replace("granted=false", "granted=true", StringComparison.Ordinal)
            .Replace("TEMP", "granted=false", StringComparison.Ordinal);

        var report = HelperAppService.ParsePermissionDump(swapped);

        Assert.False(report.AllGranted);
        Assert.False(report.CanReadPrivilegedCategories);
        Assert.Empty(report.Granted);
    }

    [Fact]
    public void APermissionAbsentFromUserZeroIsNotSatisfiedByASecondaryUsersGrant()
    {
        // The case that actually requires section anchoring: user 0's block simply omits
        // READ_SMS, while user 10 holds it. An unanchored scan finds user 10's granted=true
        // and wrongly reports the helper as able to read SMS on the user PhoneFork targets.
        var dump = """
            Packages:
              Package [com.sysadmindoc.phonefork.helper]
                User 0:
                  runtime permissions:
                    android.permission.READ_CALL_LOG: granted=true
                    android.permission.WRITE_CALL_LOG: granted=true
                    android.permission.READ_CONTACTS: granted=true
                    android.permission.WRITE_CONTACTS: granted=true
                User 10:
                  runtime permissions:
                    android.permission.READ_SMS: granted=true
            """;

        var report = HelperAppService.ParsePermissionDump(dump);

        Assert.DoesNotContain("android.permission.READ_SMS", report.Granted);
        Assert.Equal("not-listed", report.Failed["android.permission.READ_SMS"]);
        Assert.False(report.CanReadPrivilegedCategories);
    }

    [Fact]
    public void FallsBackToTheWholeDumpWhenNoRuntimeSectionHeaderExists()
    {
        // Older and some OEM dumpsys layouts omit the header; the parser must still work.
        var headerless = "  android.permission.READ_SMS: granted=true\n";

        var report = HelperAppService.ParsePermissionDump(headerless);

        Assert.Contains("android.permission.READ_SMS", report.Granted);
    }
}
