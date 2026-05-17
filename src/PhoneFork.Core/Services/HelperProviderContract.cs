using System.Text.Json;

namespace PhoneFork.Core.Services;

/// <summary>
/// Versioned host-side contract for rows emitted by PhoneForkHelper ContentProviders.
/// The Android `content query` command returns a single `json` column; this helper
/// extracts and validates that JSON before the rest of the host trusts it.
/// </summary>
public static class HelperProviderContract
{
    public const string Schema = "phonefork.helper.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string BuildQueryUri(string authority, string? path = null, int? limit = null, int? offset = null)
    {
        if (!HelperAppService.Authorities.Contains(authority))
            throw new ArgumentException($"Unknown helper authority: {authority}", nameof(authority));

        var uri = $"content://{HelperAppService.AuthorityPrefix}.{authority}";
        if (!string.IsNullOrWhiteSpace(path))
            uri += "/" + Uri.EscapeDataString(path.Trim('/'));

        var query = new List<string>();
        if (limit is not null) query.Add($"limit={limit.Value}");
        if (offset is not null) query.Add($"offset={offset.Value}");
        return query.Count == 0 ? uri : $"{uri}?{string.Join("&", query)}";
    }

    public static string? ExtractJsonFromContentQuery(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        var trimmed = output.Trim();
        var idx = trimmed.IndexOf("json=", StringComparison.Ordinal);
        return idx < 0 ? trimmed : trimmed[(idx + "json=".Length)..].Trim();
    }

    public static HelperProviderEnvelope ParseEnvelope(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException("Helper provider returned an empty JSON payload.");

        HelperProviderEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<HelperProviderEnvelope>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Helper provider returned malformed JSON.", ex);
        }

        if (envelope is null)
            throw new FormatException("Helper provider returned no envelope.");
        if (!string.Equals(envelope.Schema, Schema, StringComparison.Ordinal))
            throw new FormatException($"Unsupported helper provider schema: {envelope.Schema}");
        if (!HelperAppService.Authorities.Contains(envelope.Authority))
            throw new FormatException($"Unknown helper authority in envelope: {envelope.Authority}");
        if (envelope.Items.ValueKind is not JsonValueKind.Array)
            throw new FormatException("Helper provider envelope must contain an items array.");

        return envelope;
    }

    public static bool TryParseEnvelope(string? json, out HelperProviderEnvelope? envelope)
    {
        envelope = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            envelope = ParseEnvelope(json);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record HelperProviderEnvelope
{
    public string Schema { get; init; } = "";
    public string Authority { get; init; } = "";
    public string Status { get; init; } = "";
    public string Mode { get; init; } = "";
    public int Count { get; init; }
    public int? NextOffset { get; init; }
    public JsonElement Items { get; init; }
    public JsonElement Capabilities { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public HelperProviderError? Error { get; init; }

    public bool IsOk => string.Equals(Status, "ok", StringComparison.Ordinal);
}

public sealed record HelperProviderError
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}
