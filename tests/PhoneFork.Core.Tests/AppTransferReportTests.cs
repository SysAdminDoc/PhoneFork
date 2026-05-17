using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class AppTransferReportTests
{
    [Fact]
    public void AssessWarnsWhenPrivateDataAndExternalPayloadNeedOtherFlows()
    {
        var app = new AppInfo
        {
            PackageName = "com.example.game",
            Label = "Example Game",
            VersionName = "1.2.3",
            VersionCode = 123,
            RemoteApkPaths = ["/data/app/com.example.game/base.apk"],
            TotalSizeBytes = 10 * 1024 * 1024,
        };
        var backup = new BackupCapability(
            PackageId: app.PackageName,
            AllowBackup: false,
            HasDataExtractionRules: false,
            HasCrossPlatformTransfer: false,
            BackupAgentName: null,
            LastBackupAt: null);
        var external = new AppExternalDataProbe(
            app.PackageName,
            new AppExternalDataLocation("obb", "/sdcard/Android/obb/com.example.game", true, 50 * 1024 * 1024, 2, null),
            new AppExternalDataLocation("external-data", "/sdcard/Android/data/com.example.game", false, null, null, null));

        var report = AppTransferReportService.Assess(app, backup, external);

        Assert.Contains(report.Facets, f => f.Area == "APK install" && f.Readiness == AppTransferReadiness.Supported);
        Assert.Contains(report.Facets, f => f.Area == "Private app data" && f.Readiness == AppTransferReadiness.Unsupported);
        Assert.Contains(report.Facets, f => f.Area == "OBB/external app data" && f.Readiness == AppTransferReadiness.Partial);
        Assert.Contains(report.Warnings, w => w.Contains("private data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Warnings, w => w.Contains("OBB", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseProbeOutputHandlesPresentAndMissingLocations()
    {
        var present = AppTransferReportService.ParseProbeOutput(
            "obb",
            "/sdcard/Android/obb/com.example",
            "present files=3 kb=1024");
        var missing = AppTransferReportService.ParseProbeOutput(
            "external-data",
            "/sdcard/Android/data/com.example",
            "missing");

        Assert.True(present.Exists);
        Assert.Equal(3, present.FileCount);
        Assert.Equal(1024 * 1024, present.Bytes);
        Assert.False(missing.Exists);
        Assert.Null(missing.Error);
    }

    [Fact]
    public async Task PackageFileIntegrityComputesSha256AndLength()
    {
        var path = Path.Combine(Path.GetTempPath(), $"phonefork-apk-integrity-{Guid.NewGuid():N}.apk");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3, 4 });

            var artifact = await PackageFileIntegrity.FromFileAsync("/data/app/base.apk", path);

            Assert.Equal(4, artifact.Bytes);
            Assert.Equal("9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a", artifact.Sha256);
            Assert.Equal("/data/app/base.apk", artifact.RemotePath);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
