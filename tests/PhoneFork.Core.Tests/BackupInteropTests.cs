using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.Core.Tests;

public class AppManagerBackupRoundTripTests : IDisposable
{
    private readonly string _root;
    private static ILogger NullLog() => new LoggerConfiguration().CreateLogger();

    public AppManagerBackupRoundTripTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"phonefork-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public async Task WriterThenReaderRoundTripsAnApk()
    {
        // Synth an APK on disk — content is irrelevant for the layout test.
        var srcApk = Path.Combine(_root, "fake-base.apk");
        await File.WriteAllBytesAsync(srcApk, new byte[] { 0x50, 0x4b, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

        var app = new AppInfo
        {
            PackageName = "com.example.fake",
            Label = "Fake",
            VersionName = "1.2.3",
            VersionCode = 123,
            RemoteApkPaths = new[] { "/data/app/com.example.fake/base.apk" },
            TotalSizeBytes = 14,
            IsSystem = false,
        };

        var writer = new AppManagerBackupWriter(NullLog());
        var dir = await writer.WriteAsync(_root, "R5CY34G070L", app, new[] { srcApk }, "0.8.0", default);
        Assert.True(File.Exists(Path.Combine(dir, "meta.am.v5")));
        Assert.True(File.Exists(Path.Combine(dir, "checksums.txt")));
        Assert.True(File.Exists(Path.Combine(dir, "fake-base.apk")));

        var reader = new AppManagerBackupReader(NullLog());
        var handle = await reader.ReadAsync(dir);
        Assert.Equal("com.example.fake", handle.Meta.PackageName);
        Assert.Equal(5, handle.Meta.MetaVersion);
        Assert.Single(handle.Meta.Apks);
        Assert.Equal("fake-base.apk", handle.Meta.Apks[0].FileName);

        // Hashed serial only — no raw on disk.
        var diskContents = File.ReadAllText(Path.Combine(dir, "meta.am.v5"));
        Assert.DoesNotContain("R5CY34G070L", diskContents);
        Assert.Contains(SerialHash.Of("R5CY34G070L"), diskContents);
    }

    [Fact]
    public async Task ReaderDetectsCorruptedApk()
    {
        var srcApk = Path.Combine(_root, "fake-base.apk");
        await File.WriteAllBytesAsync(srcApk, new byte[] { 1, 2, 3, 4 });

        var app = new AppInfo
        {
            PackageName = "com.example.bad",
            Label = "Bad",
            VersionName = "1.0",
            VersionCode = 1,
            RemoteApkPaths = new[] { "/data/app/com.example.bad/base.apk" },
            TotalSizeBytes = 4,
            IsSystem = false,
        };

        var writer = new AppManagerBackupWriter(NullLog());
        var dir = await writer.WriteAsync(_root, "R5CY", app, new[] { srcApk }, "0.8.0", default);

        // Tamper with the APK after the writer ran.
        var apkInBackup = Path.Combine(dir, "fake-base.apk");
        await File.WriteAllBytesAsync(apkInBackup, new byte[] { 0xff, 0xfe, 0xfd, 0xfc });

        var reader = new AppManagerBackupReader(NullLog());
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(dir));
    }
}

public class RetentionSweepTests : IDisposable
{
    private readonly string _root;
    private static ILogger NullLog() => new LoggerConfiguration().CreateLogger();

    public RetentionSweepTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"phonefork-retention-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private string Touch(string subdir)
    {
        var dir = Path.Combine(_root, subdir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "x.txt"), subdir);
        return dir;
    }

    [Fact]
    public void NoLimitsKeepsEverything()
    {
        var dirs = new[] { Touch("a"), Touch("b"), Touch("c") };
        var plan = new BackupRetentionSweeper(NullLog()).Plan(dirs, new RetentionPolicy());
        Assert.Empty(plan);
    }

    [Fact]
    public void KeepMostRecentCountTrimsTail()
    {
        // Force a deterministic ordering by mtime.
        var first = Touch("first");
        Thread.Sleep(20);
        var second = Touch("second");
        Thread.Sleep(20);
        var third = Touch("third");

        var sweeper = new BackupRetentionSweeper(NullLog());
        var plan = sweeper.Plan(new[] { first, second, third },
            new RetentionPolicy(KeepMostRecentCount: 2));
        Assert.Single(plan);
        Assert.Equal(first, plan[0]); // oldest gets dropped
    }

    [Fact]
    public void KeepWithinDropsAged()
    {
        var ancient = Touch("ancient");
        Directory.SetCreationTimeUtc(ancient, DateTime.UtcNow - TimeSpan.FromDays(30));
        var fresh = Touch("fresh");

        var plan = new BackupRetentionSweeper(NullLog()).Plan(
            new[] { ancient, fresh },
            new RetentionPolicy(KeepWithin: TimeSpan.FromDays(7)));
        Assert.Single(plan);
        Assert.Equal(ancient, plan[0]);
    }

    [Fact]
    public void KeepUnderTotalBytesDropsOnceBudgetExceeded()
    {
        var smallDir = Touch("small");
        var largeDir = Touch("large");
        File.WriteAllBytes(Path.Combine(largeDir, "blob.bin"), new byte[1024 * 1024]); // 1 MiB
        Thread.Sleep(10);
        var newestDir = Touch("newest");

        var plan = new BackupRetentionSweeper(NullLog()).Plan(
            new[] { smallDir, largeDir, newestDir },
            new RetentionPolicy(KeepUnderTotalBytes: 512 * 1024));
        // Newest first; budget exhausted by large -> dropped.
        Assert.Contains(largeDir, plan);
    }

    [Fact]
    public void ApplyDeletesDirectories()
    {
        var keep = Touch("keep");
        Thread.Sleep(10);
        var drop = Touch("drop");
        Thread.Sleep(10);
        var newest = Touch("newest");

        var sweeper = new BackupRetentionSweeper(NullLog());
        var removed = sweeper.Apply(new[] { keep, drop, newest },
            new RetentionPolicy(KeepMostRecentCount: 2));
        Assert.False(Directory.Exists(keep));
        Assert.True(Directory.Exists(drop));
        Assert.True(Directory.Exists(newest));
        Assert.Single(removed);
    }
}
