using System.Text.Json;
using PhoneFork.Core.Services;
using Serilog;
using Serilog.Core;

namespace PhoneFork.Core.Tests;

/// <summary>
/// F112 — the helper export must follow the provider's nextOffset to completion, merge every
/// page into one document, and surface provider errors instead of writing a truncated file.
/// </summary>
public class HelperExportTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"phonefork-export-{Guid.NewGuid():N}");

    private static HelperExportService Service() => new(helper: null!, Logger.None);

    private static HelperProviderEnvelope Page(string authority, int[] ids, int? nextOffset)
    {
        var items = JsonSerializer.Deserialize<JsonElement>(
            "[" + string.Join(",", ids.Select(i => $"{{\"id\":{i}}}")) + "]");
        return new HelperProviderEnvelope
        {
            Schema = HelperProviderContract.Schema,
            Authority = authority,
            Status = "ok",
            Mode = "export",
            Count = ids.Length,
            NextOffset = nextOffset,
            Items = items,
        };
    }

    [Fact]
    public async Task FollowsNextOffsetToCompletionAndMergesEveryPage()
    {
        var pagesRequested = new List<int>();
        var result = await Service().ExportAsync("sms", (offset, _) =>
        {
            pagesRequested.Add(offset);
            return Task.FromResult<HelperProviderEnvelope?>(offset switch
            {
                0 => Page("sms", new[] { 1, 2 }, nextOffset: 2),
                2 => Page("sms", new[] { 3, 4 }, nextOffset: 4),
                _ => Page("sms", new[] { 5 }, nextOffset: null),
            });
        }, outputPath: null);

        Assert.True(result.Success);
        Assert.Equal(new[] { 0, 2, 4 }, pagesRequested);
        Assert.Equal(3, result.Pages);
        Assert.Equal(5, result.ItemCount);
    }

    [Fact]
    public async Task StopsWhenTheProviderRepeatsAnOffsetRatherThanLoopingForever()
    {
        var calls = 0;
        var result = await Service().ExportAsync("contacts", (offset, _) =>
        {
            calls++;
            // A provider that always reports the same offset would otherwise page forever.
            return Task.FromResult<HelperProviderEnvelope?>(Page("contacts", new[] { 1 }, nextOffset: offset));
        }, outputPath: null);

        Assert.True(result.Success);
        Assert.Equal(1, calls);
        Assert.Equal(1, result.ItemCount);
    }

    [Fact]
    public async Task ReportsAProviderErrorInsteadOfWritingATruncatedFile()
    {
        var outPath = Path.Combine(_tempDir, "sms.json");
        var result = await Service().ExportAsync("sms", (offset, _) =>
            Task.FromResult<HelperProviderEnvelope?>(offset == 0
                ? Page("sms", new[] { 1, 2 }, nextOffset: 2)
                : new HelperProviderEnvelope
                {
                    Schema = HelperProviderContract.Schema,
                    Authority = "sms",
                    Status = "error",
                    Mode = "export",
                    Items = JsonSerializer.Deserialize<JsonElement>("[]"),
                    Error = new HelperProviderError { Code = "permission-denied", Message = "READ_SMS not granted" },
                }),
            outPath);

        Assert.False(result.Success);
        Assert.Contains("permission-denied", result.Error);
        Assert.Contains("READ_SMS not granted", result.Error);
        Assert.Null(result.OutputPath);
        Assert.False(File.Exists(outPath), "a failed export must not leave a partial file behind");
    }

    [Fact]
    public async Task ReportsAnUnparseableEnvelopeAsAFailure()
    {
        var result = await Service().ExportAsync("calllog",
            (_, _) => Task.FromResult<HelperProviderEnvelope?>(null), outputPath: null);

        Assert.False(result.Success);
        Assert.Equal("invalid-or-empty-provider-envelope", result.Error);
    }

    [Fact]
    public async Task WritesEveryMergedRowAndDeduplicatesWarnings()
    {
        var outPath = Path.Combine(_tempDir, "nested", "wifi.json");
        var result = await Service().ExportAsync("wifi", (offset, _) =>
        {
            var page = Page("wifi", offset == 0 ? new[] { 1, 2 } : new[] { 3 }, offset == 0 ? 2 : null);
            return Task.FromResult<HelperProviderEnvelope?>(page with
            {
                Warnings = new[] { "Android does not expose saved Wi-Fi PSKs to a normal helper APK." },
            });
        }, outPath);

        Assert.True(result.Success);
        Assert.Equal(outPath, result.OutputPath);
        Assert.True(File.Exists(outPath), "the export directory should be created on demand");

        // The same warning arrived on both pages and must be recorded once.
        Assert.Single(result.Warnings);

        using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
        var root = doc.RootElement;
        Assert.Equal(HelperProviderContract.Schema, root.GetProperty("schema").GetString());
        Assert.Equal("wifi", root.GetProperty("authority").GetString());
        Assert.Equal(3, root.GetProperty("count").GetInt32());
        Assert.Equal(3, root.GetProperty("items").GetArrayLength());
        Assert.Single(root.GetProperty("warnings").EnumerateArray());
        Assert.Equal(
            new[] { 1, 2, 3 },
            root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task HandlesAnEmptyProviderResult()
    {
        var outPath = Path.Combine(_tempDir, "dictionary.json");
        var result = await Service().ExportAsync("dictionary",
            (_, _) => Task.FromResult<HelperProviderEnvelope?>(Page("dictionary", Array.Empty<int>(), null)),
            outPath);

        Assert.True(result.Success);
        Assert.Equal(0, result.ItemCount);

        using var doc = JsonDocument.Parse(File.ReadAllText(outPath));
        Assert.Equal(0, doc.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task HonoursCancellationBetweenPages()
    {
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service().ExportAsync("sms", (offset, _) =>
            {
                cts.Cancel();
                return Task.FromResult<HelperProviderEnvelope?>(Page("sms", new[] { 1 }, nextOffset: offset + 1));
            }, outputPath: null, progress: null, cts.Token));
    }

    [Fact]
    public async Task RejectsAnAuthorityTheHelperDoesNotDeclare()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Service().ExportAsync(device: default!, authority: "banking", outputPath: null));
    }

    [Fact]
    public void DefaultFileNameIsSafeAndPerAuthority()
    {
        foreach (var authority in HelperAppService.Authorities)
        {
            var name = HelperExportService.DefaultFileName(authority);
            Assert.EndsWith(".json", name);
            Assert.Equal(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()));
            Assert.Contains(authority, name);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
