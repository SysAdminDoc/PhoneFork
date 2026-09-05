using System.Text.Json;
using AdvancedSharpAdbClient.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Result of exporting one helper authority (F112).
/// </summary>
public sealed record HelperExportResult(
    string Authority,
    int ItemCount,
    int Pages,
    IReadOnlyList<string> Warnings,
    string? OutputPath,
    string? Error)
{
    public bool Success => Error is null;
}

/// <summary>
/// Drives a helper ContentProvider to completion and writes what it returns to disk (F112).
///
/// <see cref="HelperAppService.QueryAsync"/> returns one page at a time and reports the next
/// offset in the v1 envelope; this service follows <c>nextOffset</c> until the provider stops
/// paging, merges every page's items into one array, and persists the result as a single JSON
/// document alongside the provider's own warnings.
/// </summary>
public sealed class HelperExportService
{
    /// <summary>Provider page size. Matches the helper's own default; its ceiling is 2,000.</summary>
    public const int PageSize = 500;

    /// <summary>
    /// Upper bound on pages followed for one authority. A provider that keeps returning a
    /// nextOffset would otherwise loop forever; at the default page size this still allows
    /// half a million rows.
    /// </summary>
    public const int MaxPages = 1_000;

    private readonly HelperAppService _helper;
    private readonly ILogger _log;

    public HelperExportService(HelperAppService helper, ILogger log)
    {
        _helper = helper;
        _log = log.ForContext<HelperExportService>();
    }

    /// <summary>
    /// Pages one authority to completion. When <paramref name="outputPath"/> is given the
    /// merged document is written there; otherwise the rows are counted but not persisted.
    /// </summary>
    public async Task<HelperExportResult> ExportAsync(
        DeviceData device,
        string authority,
        string? outputPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!HelperAppService.Authorities.Contains(authority))
            throw new ArgumentException($"Unknown helper authority: {authority}", nameof(authority));

        return await ExportAsync(
            authority,
            (offset, token) => _helper.QueryAsync(device, authority, PageSize, offset, token),
            outputPath,
            progress,
            ct);
    }

    /// <summary>
    /// Paging loop, separated from the ADB call so <c>nextOffset</c> following is testable
    /// without a device. <paramref name="fetchPage"/> receives the offset to request.
    /// </summary>
    public async Task<HelperExportResult> ExportAsync(
        string authority,
        Func<int, CancellationToken, Task<HelperProviderEnvelope?>> fetchPage,
        string? outputPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var items = new List<JsonElement>();
        var warnings = new List<string>();
        var offset = 0;
        var pages = 0;

        while (pages < MaxPages)
        {
            ct.ThrowIfCancellationRequested();

            var envelope = await fetchPage(offset, ct);
            if (envelope is null)
                return Failure(authority, "invalid-or-empty-provider-envelope", outputPath, pages, items, warnings);

            if (!envelope.IsOk)
            {
                var code = envelope.Error?.Code ?? envelope.Status;
                var message = envelope.Error?.Message ?? "provider returned a non-ok status";
                return Failure(authority, $"{code}: {message}", outputPath, pages, items, warnings);
            }

            pages++;
            foreach (var item in envelope.Items.EnumerateArray())
                items.Add(item.Clone());

            foreach (var warning in envelope.Warnings)
                if (!warnings.Contains(warning, StringComparer.Ordinal))
                    warnings.Add(warning);

            progress?.Report($"{authority}: {items.Count} row(s) after page {pages}");

            if (envelope.NextOffset is not { } next || next <= offset)
                break;

            offset = next;
        }

        if (pages >= MaxPages)
            warnings.Add($"Stopped after {MaxPages} pages; the provider may have more rows.");

        _log.Information("Helper export {Authority}: {Count} item(s) over {Pages} page(s)",
            authority, items.Count, pages);

        var written = outputPath is null ? null : await WriteAsync(authority, items, warnings, outputPath, ct);
        return new HelperExportResult(authority, items.Count, pages, warnings, written, Error: null);
    }

    private HelperExportResult Failure(
        string authority,
        string error,
        string? outputPath,
        int pages,
        List<JsonElement> items,
        List<string> warnings)
    {
        _log.Warning("Helper export {Authority} failed after {Pages} page(s): {Error}", authority, pages, error);
        return new HelperExportResult(authority, items.Count, pages, warnings, OutputPath: null, Error: error);
    }

    private static async Task<string> WriteAsync(
        string authority,
        IReadOnlyList<JsonElement> items,
        IReadOnlyList<string> warnings,
        string outputPath,
        CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(outputPath);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteString("schema", HelperProviderContract.Schema);
        writer.WriteString("authority", authority);
        writer.WriteString("exportedAt", DateTimeOffset.UtcNow.ToString("O"));
        writer.WriteNumber("count", items.Count);

        writer.WriteStartArray("warnings");
        foreach (var warning in warnings) writer.WriteStringValue(warning);
        writer.WriteEndArray();

        writer.WriteStartArray("items");
        foreach (var item in items) item.WriteTo(writer);
        writer.WriteEndArray();

        writer.WriteEndObject();
        await writer.FlushAsync(ct);
        return Path.GetFullPath(outputPath);
    }

    /// <summary>
    /// Default file name for an authority's export, safe on Windows.
    /// </summary>
    public static string DefaultFileName(string authority)
        => LocalPathNames.SafeFileName($"helper-{authority}", fallback: "helper-export") + ".json";
}
