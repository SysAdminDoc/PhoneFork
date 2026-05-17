using PhoneFork.Core.Logging;
using PhoneFork.Core.Services;

namespace PhoneFork.Core.Tests;

public sealed class AuditLoggerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"phonefork-audit-{Guid.NewGuid():N}");

    [Fact]
    public void AuditLoggerWritesCompactJsonAndHashesSerials()
    {
        var log = AuditLogger.Create(_tempDir);
        log.Information("device seen {serial} {device}", "R5CY34G070L", "R5CY34G070L");
        (log as IDisposable)?.Dispose();

        var file = Assert.Single(Directory.GetFiles(_tempDir, "audit-*.log"));
        var text = File.ReadAllText(file);

        Assert.Contains("\"app\":\"PhoneFork\"", text);
        Assert.Contains(SerialHash.Of("R5CY34G070L"), text);
        Assert.DoesNotContain("R5CY34G070L", text);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }
}
