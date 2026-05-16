using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public class BackupCapabilityFindingTests
{
    [Fact]
    public void AllowBackupFalseEmitsWarning()
    {
        var cap = new BackupCapability("com.example.app",
            AllowBackup: false,
            HasDataExtractionRules: false,
            HasCrossPlatformTransfer: false,
            BackupAgentName: null,
            LastBackupAt: null);
        var finding = BackupCapabilityService.ToFinding(cap);
        Assert.NotNull(finding);
        Assert.Equal(HonestyLevel.Warning, finding!.Level);
        Assert.Equal("com.example.app", finding.PackageId);
    }

    [Fact]
    public void LegacyDefaultAgentEmitsInfo()
    {
        var cap = new BackupCapability("com.example.app",
            AllowBackup: true,
            HasDataExtractionRules: false,
            HasCrossPlatformTransfer: false,
            BackupAgentName: null,
            LastBackupAt: null);
        var finding = BackupCapabilityService.ToFinding(cap);
        Assert.NotNull(finding);
        Assert.Equal(HonestyLevel.Info, finding!.Level);
    }

    [Fact]
    public void GoodCitizenProducesNoFinding()
    {
        var cap = new BackupCapability("com.example.app",
            AllowBackup: true,
            HasDataExtractionRules: true,
            HasCrossPlatformTransfer: true,
            BackupAgentName: "com.example.app.Backup",
            LastBackupAt: DateTimeOffset.UtcNow);
        var finding = BackupCapabilityService.ToFinding(cap);
        Assert.Null(finding);
    }

    [Fact]
    public void ParticipatesInAutoBackupReflectsManifest()
    {
        Assert.True(new BackupCapability("a", AllowBackup: true, HasDataExtractionRules: true,
            HasCrossPlatformTransfer: false, BackupAgentName: null, LastBackupAt: null).ParticipatesInAutoBackup);
        Assert.False(new BackupCapability("a", AllowBackup: false, HasDataExtractionRules: false,
            HasCrossPlatformTransfer: false, BackupAgentName: null, LastBackupAt: null).ParticipatesInAutoBackup);
    }
}

public class OpenArchiveManifestTests
{
    [Fact]
    public void ManifestRoundTripsThroughSystemTextJson()
    {
        var original = new OpenArchiveManifest
        {
            CreatedAt = DateTimeOffset.Parse("2026-05-16T20:00:00Z"),
            ToolVersion = "0.7.0",
            MigrationId = "mig-abcd-1234",
            Source = new ArchiveEndpointInfo
            {
                DeviceHash = SerialHash.Of("R5CY34G070L"),
                Label = "Galaxy S25 Ultra",
                AndroidVersion = "16",
                OneUiVersion = "80500",
            },
            Categories = new[]
            {
                new CategoryEntry { Name = "contacts", File = "contacts.vcf", Sha256 = "abcd", Bytes = 1234, Rows = 56 },
            },
            Notes = new[] { "Wi-Fi PSK export requires Shizuku." },
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<OpenArchiveManifest>(json)!;

        Assert.Equal(original.Schema, deserialized.Schema);
        Assert.Equal(original.MigrationId, deserialized.MigrationId);
        Assert.Single(deserialized.Categories);
        Assert.Equal("contacts.vcf", deserialized.Categories[0].File);
        // Hash carried through, not the raw serial.
        Assert.Equal(SerialHash.Of("R5CY34G070L"), deserialized.Source.DeviceHash);
        Assert.DoesNotContain("R5CY34G070L", json);
    }

    [Fact]
    public void SchemaConstantIsStable()
    {
        // Bumping the schema is a breaking change for downstream consumers. If this test
        // fails, write a new schema string and add a migration note rather than silently
        // upgrading.
        var manifest = new OpenArchiveManifest
        {
            CreatedAt = DateTimeOffset.UtcNow,
            ToolVersion = "0.7.0",
            MigrationId = "x",
        };
        Assert.Equal("phonefork-open-archive-v1", manifest.Schema);
    }
}

public class ProviderCallAuditTests
{
    [Fact]
    public void EndIsIdempotent()
    {
        using var scope = ProviderCallAudit.Begin("test.op", "R5CY", "RF22", "mig-1",
            new Serilog.LoggerConfiguration().CreateLogger());
        scope.End(ok: true, rowsTouched: 1);
        scope.End(ok: false); // second call should be a no-op
    }

    [Fact]
    public void DisposeEndsWithoutExplicitOutcome()
    {
        var log = new Serilog.LoggerConfiguration().CreateLogger();
        ProviderCallAudit.Scope? captured;
        using (var scope = ProviderCallAudit.Begin("test.op", null, null, null, log))
        {
            captured = scope;
        }
        Assert.NotNull(captured);
    }
}
