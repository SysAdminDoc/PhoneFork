using System.Text;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.Core.Tests;

public class AndroidBackupReaderTests : IDisposable
{
    private readonly string _dir;
    private static ILogger NullLog() => new LoggerConfiguration().CreateLogger();

    public AndroidBackupReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"phonefork-ab-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void IdentifiesPlainUncompressedAbHeader()
    {
        var path = Path.Combine(_dir, "fake.ab");
        // Minimal valid header: magic + version + compression + encryption tag.
        var header = Encoding.ASCII.GetBytes("ANDROID BACKUP\n5\n0\nnone\n");
        File.WriteAllBytes(path, header);

        var reader = new AndroidBackupReader(NullLog());
        var parsed = reader.Sniff(path);
        Assert.NotNull(parsed);
        Assert.Equal(5, parsed!.FormatVersion);
        Assert.False(parsed.Compressed);
        Assert.Equal("none", parsed.EncryptionTag);
        Assert.False(parsed.HasEncryptionKeyBlock);
        Assert.True(reader.IsAdbBackup(path));
    }

    [Fact]
    public void IdentifiesCompressedEncryptedAbHeader()
    {
        var path = Path.Combine(_dir, "encrypted.ab");
        var header = Encoding.ASCII.GetBytes(
            "ANDROID BACKUP\n5\n1\nAES-256\nsalt1\nsalt2\n10000\nuserkey\nmasterkey\n");
        File.WriteAllBytes(path, header);

        var reader = new AndroidBackupReader(NullLog());
        var parsed = reader.Sniff(path);
        Assert.NotNull(parsed);
        Assert.Equal(5, parsed!.FormatVersion);
        Assert.True(parsed.Compressed);
        Assert.Equal("AES-256", parsed.EncryptionTag);
        Assert.True(parsed.HasEncryptionKeyBlock);
    }

    [Fact]
    public void NonAbFileReturnsNull()
    {
        var path = Path.Combine(_dir, "not-a-backup.bin");
        File.WriteAllBytes(path, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0, 0, 0 });
        var reader = new AndroidBackupReader(NullLog());
        Assert.Null(reader.Sniff(path));
        Assert.False(reader.IsAdbBackup(path));
    }

    [Fact]
    public void MissingFileReturnsNull()
    {
        var reader = new AndroidBackupReader(NullLog());
        Assert.Null(reader.Sniff(Path.Combine(_dir, "missing.ab")));
    }
}

public class OpenAndroidBackupReaderTests : IDisposable
{
    private readonly string _dir;
    private static ILogger NullLog() => new LoggerConfiguration().CreateLogger();

    public OpenAndroidBackupReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"phonefork-oab-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void DetectsArchiveWithJsonSidecar()
    {
        var archive = Path.Combine(_dir, "backup.7z");
        File.WriteAllBytes(archive, new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C });
        File.WriteAllText(Path.Combine(_dir, "backup-metadata.json"), "{}");

        var reader = new OpenAndroidBackupReader(NullLog());
        var header = reader.Sniff(_dir);
        Assert.NotNull(header);
        Assert.True(header!.HasSidecar);
        Assert.Equal(6, header.ArchiveSizeBytes);
    }

    [Fact]
    public void DetectsArchiveWithTxtSidecar()
    {
        var archive = Path.Combine(_dir, "backup.7z");
        File.WriteAllBytes(archive, new byte[] { 0x37, 0x7A });
        File.WriteAllText(Path.ChangeExtension(archive, ".txt"), "metadata");

        var header = new OpenAndroidBackupReader(NullLog()).Sniff(_dir);
        Assert.NotNull(header);
        Assert.True(header!.HasSidecar);
    }

    [Fact]
    public void EmptyDirReturnsNull()
    {
        Assert.Null(new OpenAndroidBackupReader(NullLog()).Sniff(_dir));
    }
}

public class CrossPlatformMetadataTests
{
    [Fact]
    public void EmptyMetadataSerializes()
    {
        var meta = new CrossPlatformMetadata
        {
            IosCompatibleApps = new[] { "com.example.app" },
            Notes = new[] { "WhatsApp uses its own iOS interop flow." },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(meta);
        Assert.Contains("com.example.app", json);
        Assert.Contains("iosCompatibleApps", json);
    }

    [Fact]
    public void ManifestExposesCrossPlatformOptional()
    {
        var manifest = new OpenArchiveManifest
        {
            CreatedAt = DateTimeOffset.UtcNow,
            ToolVersion = "0.9.0",
            MigrationId = "mig-1",
        };
        Assert.Null(manifest.CrossPlatform);
    }
}
