using System.Reflection;
using System.Text.Json.Serialization;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record MigrationReceiptDevice(
    string Role,
    string SerialHash,
    string? Label = null);

public sealed record MigrationReceiptArtifact(
    string Kind,
    string Path);

public sealed record MigrationReceiptCategory(
    string Name,
    int Planned,
    int Succeeded,
    int Skipped,
    int Failed,
    IReadOnlyList<string> FailureDetails,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<MigrationReceiptArtifact> Artifacts);

public sealed record MigrationReceipt(
    string Schema,
    string ReceiptId,
    DateTimeOffset CreatedAt,
    string ToolVersion,
    string Operation,
    bool DryRun,
    IReadOnlyList<MigrationReceiptDevice> Devices,
    IReadOnlyList<MigrationReceiptCategory> Categories,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<MigrationReceiptArtifact> Artifacts)
{
    [JsonIgnore]
    public int Failed => Categories.Sum(c => c.Failed);
}

public sealed class MigrationReceiptService
{
    public const string CurrentSchema = "phonefork.migration-receipt.v1";

    private readonly ILogger _log;
    private readonly string _receiptDirectory;

    public MigrationReceiptService(ILogger log, string? receiptDirectory = null)
    {
        _log = log.ForContext<MigrationReceiptService>();
        _receiptDirectory = receiptDirectory ?? ReceiptDirectory;
        Directory.CreateDirectory(_receiptDirectory);
    }

    public static string ReceiptDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhoneFork",
        "receipts");

    public async Task<string> WriteAsync(MigrationReceipt receipt, CancellationToken ct = default)
    {
        var safeOperation = LocalPathNames.SafeFileName(receipt.Operation, "operation");
        var path = Path.Combine(_receiptDirectory, $"{receipt.CreatedAt:yyyyMMddTHHmmss}-{safeOperation}-{receipt.ReceiptId}.json");
        await using var stream = File.Create(path);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, receipt, MediaJson.Options, ct);
        _log.Information("Migration receipt written {Path} operation={Operation} failed={Failed}",
            path, receipt.Operation, receipt.Failed);
        return path;
    }

    public static MigrationReceipt Create(
        string operation,
        bool dryRun,
        IEnumerable<MigrationReceiptDevice> devices,
        IEnumerable<MigrationReceiptCategory> categories,
        IEnumerable<string>? warnings = null,
        IEnumerable<MigrationReceiptArtifact>? artifacts = null)
        => new(
            Schema: CurrentSchema,
            ReceiptId: Guid.NewGuid().ToString("N")[..12],
            CreatedAt: DateTimeOffset.UtcNow,
            ToolVersion: ToolVersion(),
            Operation: operation,
            DryRun: dryRun,
            Devices: devices.ToList(),
            Categories: categories.ToList(),
            Warnings: (warnings ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            Artifacts: (artifacts ?? Array.Empty<MigrationReceiptArtifact>()).ToList());

    public static MigrationReceiptDevice Device(string role, DeviceData device, string? label = null)
        => new(role, SerialHash.Of(device.Serial), label);

    public static MigrationReceiptDevice Device(string role, PhoneInfo phone)
        => new(role, SerialHash.Of(phone.Serial), phone.DisplayName);

    public static MigrationReceiptCategory Category(
        string name,
        int planned,
        int succeeded,
        int skipped,
        int failed,
        IEnumerable<string>? failureDetails = null,
        IEnumerable<string>? warnings = null,
        IEnumerable<MigrationReceiptArtifact>? artifacts = null)
        => new(
            Name: name,
            Planned: planned,
            Succeeded: succeeded,
            Skipped: skipped,
            Failed: failed,
            FailureDetails: (failureDetails ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Take(100).ToList(),
            Warnings: (warnings ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Take(100).ToList(),
            Artifacts: (artifacts ?? Array.Empty<MigrationReceiptArtifact>()).ToList());

    private static string ToolVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(MigrationReceiptService).Assembly;
        var version = assembly.GetName().Version?.ToString();
        return string.IsNullOrWhiteSpace(version) ? "phonefork" : $"phonefork/{version}";
    }
}
