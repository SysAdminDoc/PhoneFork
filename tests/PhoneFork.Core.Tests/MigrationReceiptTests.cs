using System.Text.Json;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog.Core;

namespace PhoneFork.Core.Tests;

public sealed class MigrationReceiptTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"phonefork-receipts-{Guid.NewGuid():N}");

    [Fact]
    public async Task WriteAsyncPersistsReceiptWithHashedDeviceSerial()
    {
        var receipt = MigrationReceiptService.Create(
            operation: "test-operation",
            dryRun: false,
            devices: new[] { new MigrationReceiptDevice("source", SerialHash.Of("R5CY34G070L"), "Galaxy") },
            categories: new[]
            {
                MigrationReceiptService.Category(
                    "apps",
                    planned: 2,
                    succeeded: 1,
                    skipped: 0,
                    failed: 1,
                    failureDetails: new[] { "com.example: failed" },
                    artifacts: new[] { new MigrationReceiptArtifact("rollback-snapshot", "snapshot.json") }),
            },
            warnings: new[] { "test warning" });

        var path = await new MigrationReceiptService(Logger.None, _tempDir).WriteAsync(receipt);

        Assert.True(File.Exists(path));
        var json = await File.ReadAllTextAsync(path);
        Assert.Contains(MigrationReceiptService.CurrentSchema, json);
        Assert.Contains(SerialHash.Of("R5CY34G070L"), json);
        Assert.DoesNotContain("R5CY34G070L", json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("test-operation", doc.RootElement.GetProperty("Operation").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("Categories")[0].GetProperty("Failed").GetInt32());
    }

    [Fact]
    public void DeviceFromPhoneInfoHashesSerialAndKeepsDisplayLabel()
    {
        var phone = new PhoneInfo("ABC123", "Samsung", "Galaxy S25", "16", "80500", "", true);

        var device = MigrationReceiptService.Device("destination", phone);

        Assert.Equal("destination", device.Role);
        Assert.Equal(SerialHash.Of("ABC123"), device.SerialHash);
        Assert.Equal("Samsung Galaxy S25", device.Label);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
