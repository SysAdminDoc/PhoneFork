using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

/// <summary>
/// Reads region / locale / carrier info from getprop, used to surface a mismatch banner before
/// migration. CSC / locale / region mismatches are the root cause of many Samsung Wallet /
/// Pass / regional-app restore failures (community §9).
/// </summary>
public sealed class CscDiffService
{
    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public CscDiffService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<CscDiffService>();
    }

    public async Task<CscSnapshot> CaptureAsync(DeviceData device, CancellationToken ct = default)
    {
        async Task<string> Get(string key) => (await _client.ShellAsync(device, $"getprop {key}", ct)).Trim();
        return new CscSnapshot
        {
            DeviceSerial = device.Serial,
            SalesCode    = await Get("persist.sys.sales_code"),
            CountryCode  = await Get("ro.csc.country_code"),
            Locale       = await Get("persist.sys.locale"),
            Timezone     = await Get("persist.sys.timezone"),
            CarrierIso   = await Get("gsm.sim.operator.iso-country"),
        };
    }

    public CscDiffSummary Diff(CscSnapshot source, CscSnapshot dest)
    {
        return new CscDiffSummary(
            SalesCodeMismatch: !Equals(source.SalesCode, dest.SalesCode),
            CountryCodeMismatch: !Equals(source.CountryCode, dest.CountryCode),
            LocaleMismatch: !Equals(source.Locale, dest.Locale),
            TimezoneMismatch: !Equals(source.Timezone, dest.Timezone),
            CarrierMismatch: !Equals(source.CarrierIso, dest.CarrierIso),
            Source: source,
            Dest: dest);
    }

    private static bool Equals(string a, string b) =>
        string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
}

public sealed record CscDiffSummary(
    bool SalesCodeMismatch,
    bool CountryCodeMismatch,
    bool LocaleMismatch,
    bool TimezoneMismatch,
    bool CarrierMismatch,
    CscSnapshot Source,
    CscSnapshot Dest)
{
    public bool AnyMismatch =>
        SalesCodeMismatch || CountryCodeMismatch || LocaleMismatch || TimezoneMismatch || CarrierMismatch;
}
