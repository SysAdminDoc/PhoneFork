using System.Text.RegularExpressions;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using PhoneFork.Core.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record RoleApplyResult(int Applied, int Failed, IReadOnlyList<(string Role, string Pkg, string Error)> Failures);

/// <summary>
/// Reads + applies AOSP default-app roles via <c>cmd role get-role-holders &lt;role&gt;</c> and
/// <c>cmd role add-role-holder &lt;role&gt; &lt;pkg&gt;</c>. Works from shell UID on Android 11+.
/// </summary>
public sealed class RoleService
{
    private static readonly Regex CmdRolePackageLine = new(
        @"^\s*package:(?<pkg>[A-Za-z0-9_.]+)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public RoleService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<RoleService>();
    }

    public async Task<RoleSnapshot> SnapshotAsync(DeviceData device, IEnumerable<string>? roles = null, CancellationToken ct = default)
    {
        var list = (roles ?? DefaultRoles.All).ToList();
        var holders = new List<RoleHolder>(list.Count);
        foreach (var role in list)
        {
            ct.ThrowIfCancellationRequested();
            // `cmd role get-role-holders <role>` prints either nothing, or "package:<pkg>" possibly
            // followed by other text, or "[<pkg>]" depending on Android version. Match either.
            var output = await _client.ShellAsync(device, $"cmd role get-role-holders --user 0 {role}", ct);
            string? holder = null;
            var m = CmdRolePackageLine.Match(output ?? "");
            if (m.Success)
            {
                holder = m.Groups["pkg"].Value;
            }
            else
            {
                // Some Android builds print the package name in square brackets without "package:".
                var bracket = Regex.Match(output ?? "", @"\[(?<pkg>[A-Za-z0-9_.]+)\]");
                if (bracket.Success) holder = bracket.Groups["pkg"].Value;
                else if (!string.IsNullOrWhiteSpace(output))
                {
                    var trimmed = output!.Trim();
                    if (Regex.IsMatch(trimmed, @"^[A-Za-z0-9_.]+$")) holder = trimmed;
                }
            }
            holders.Add(new RoleHolder(role, holder));
        }
        _log.Information("Role snapshot {Serial}: {Held} of {Total} roles held.",
            device.Serial, holders.Count(h => h.HolderPackage is not null), holders.Count);
        return new RoleSnapshot { DeviceSerial = device.Serial, CapturedAt = DateTimeOffset.UtcNow, Holders = holders };
    }

    public async Task<RoleApplyResult> ApplyAsync(
        DeviceData destination,
        IEnumerable<(string Role, string Pkg)> assignments,
        bool dryRun,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        int applied = 0, failed = 0;
        var failures = new List<(string, string, string)>();
        foreach (var (role, pkg) in assignments)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"cmd role add-role-holder --user 0 {role} {pkg}");
            if (dryRun) { applied++; continue; }
            try
            {
                var output = await _client.ShellAsync(destination, $"cmd role add-role-holder --user 0 {role} {pkg}", ct);
                if (string.IsNullOrWhiteSpace(output) || !output.Contains("Exception", StringComparison.Ordinal))
                    applied++;
                else
                {
                    failed++;
                    failures.Add((role, pkg, output.Trim()));
                    _log.Warning("cmd role add-role-holder {Role} {Pkg} failed: {Output}", role, pkg, output.Trim());
                }
            }
            catch (Exception ex)
            {
                failed++;
                failures.Add((role, pkg, ex.Message));
                _log.Warning(ex, "cmd role add-role-holder {Role} {Pkg} threw", role, pkg);
            }
        }
        return new RoleApplyResult(applied, failed, failures);
    }

    /// <summary>
    /// Grants a runtime permission and/or sets an <c>appops</c> mode. Either may fail silently; we
    /// return the raw shell output for diagnostics.
    /// </summary>
    public async Task<string> GrantAsync(DeviceData device, string pkg, string permission, CancellationToken ct = default)
        => await _client.ShellAsync(device, $"pm grant {pkg} {permission}", ct);

    public async Task<string> SetAppOpAsync(DeviceData device, string pkg, string op, string mode, CancellationToken ct = default)
        => await _client.ShellAsync(device, $"appops set {pkg} {op} {mode}", ct);
}
