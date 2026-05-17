using System.Globalization;
using System.Text.RegularExpressions;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Serilog;

namespace PhoneFork.Core.Services;

public sealed record AndroidUserProfile(
    int UserId,
    string Name,
    string FlagsRaw,
    bool IsRunning,
    bool IsCurrent,
    bool IsManagedProfile,
    bool IsGuest,
    bool IsRestricted,
    bool IsSystem)
{
    public string Describe()
    {
        var traits = new List<string>();
        if (IsCurrent) traits.Add("current");
        if (IsManagedProfile) traits.Add("managed/work");
        if (IsGuest) traits.Add("guest");
        if (IsRestricted) traits.Add("restricted");
        if (IsSystem) traits.Add("system");
        if (IsRunning) traits.Add("running");
        var suffix = traits.Count == 0 ? "" : $" ({string.Join(", ", traits)})";
        return $"user {UserId} {Name}{suffix}";
    }
}

public sealed record AndroidUserProfileReport(
    IReadOnlyList<AndroidUserProfile> Users,
    int? CurrentUserId,
    string RawUsersOutput,
    string RawCurrentUserOutput,
    string? Error)
{
    public bool IsReliable => Error is null && Users.Count > 0;

    public IReadOnlyList<string> PrimaryUserWriteBlockers()
    {
        var blockers = new List<string>();
        if (!IsReliable)
            blockers.Add(Error is null
                ? "Android user/profile topology could not be verified."
                : $"Android user/profile topology could not be verified: {Error}");

        if (CurrentUserId is { } current && current != 0)
            blockers.Add($"Current Android user is {current}, but PhoneFork write paths target user 0.");

        if (Users.Count > 0 && Users.All(u => u.UserId != 0))
            blockers.Add("Primary Android user 0 was not reported by the device.");

        foreach (var user in Users.Where(u => u.UserId != 0 && u.IsManagedProfile))
            blockers.Add($"Managed/work profile detected: {user.Describe()}.");

        foreach (var user in Users.Where(u => u.UserId != 0 && !u.IsManagedProfile))
            blockers.Add($"Additional Android user detected: {user.Describe()}.");

        return blockers.Distinct(StringComparer.Ordinal).ToList();
    }

    public string Describe()
        => Users.Count == 0 ? "(no users parsed)" : string.Join("; ", Users.Select(u => u.Describe()));
}

public sealed class AndroidUserProfileGuardException : InvalidOperationException
{
    public AndroidUserProfileGuardException(string message, AndroidUserProfileReport report)
        : base(message)
    {
        Report = report;
    }

    public AndroidUserProfileReport Report { get; }
}

public sealed partial class AndroidUserProfileService
{
    private const int FlagGuest = 0x00000004;
    private const int FlagRestricted = 0x00000008;
    private const int FlagManagedProfile = 0x00000020;
    private const int FlagSystem = 0x00000800;

    private readonly IAdbClient _client;
    private readonly ILogger _log;

    public AndroidUserProfileService(IAdbClient client, ILogger log)
    {
        _client = client;
        _log = log.ForContext<AndroidUserProfileService>();
    }

    public async Task<AndroidUserProfileReport> ProbeAsync(DeviceData device, CancellationToken ct = default)
    {
        try
        {
            var usersOutput = await _client.ShellAsync(device, "pm list users", ct) ?? "";
            var currentOutput = await _client.ShellAsync(device, "am get-current-user", ct) ?? "";
            return ParseReport(usersOutput, currentOutput);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Android user/profile probe failed for {Serial}", device.Serial);
            return new AndroidUserProfileReport(
                Users: Array.Empty<AndroidUserProfile>(),
                CurrentUserId: null,
                RawUsersOutput: "",
                RawCurrentUserOutput: "",
                Error: ex.Message);
        }
    }

    public async Task<AndroidUserProfileReport> EnsurePrimaryUserWriteSafeAsync(
        DeviceData device,
        string action,
        bool allowMultiUser,
        CancellationToken ct = default)
    {
        var report = await ProbeAsync(device, ct);
        var blockers = report.PrimaryUserWriteBlockers();
        if (blockers.Count == 0)
            return report;

        var message =
            $"Refusing {action} because this device is not verified as primary-user-only. " +
            string.Join(" ", blockers) +
            " PhoneFork currently writes Android user 0 only; retry with --allow-multi-user only after checking work-profile and secondary-user impact.";

        if (!allowMultiUser)
            throw new AndroidUserProfileGuardException(message, report);

        _log.Warning("Proceeding with {Action} despite Android user/profile blockers: {Blockers}",
            action, string.Join(" | ", blockers));
        return report;
    }

    public static AndroidUserProfileReport ParseReport(string usersOutput, string currentUserOutput)
    {
        var current = ParseCurrentUserId(currentUserOutput);
        var users = ParseUsers(usersOutput, current).OrderBy(u => u.UserId).ToList();
        var error = current is null && !string.IsNullOrWhiteSpace(currentUserOutput)
            ? $"current user id could not be parsed from `{currentUserOutput.Trim()}`"
            : null;
        return new AndroidUserProfileReport(users, current, usersOutput, currentUserOutput, error);
    }

    public static IReadOnlyList<AndroidUserProfile> ParseUsers(string output, int? currentUserId = null)
    {
        var users = new List<AndroidUserProfile>();
        foreach (Match match in UserInfoRegex().Matches(output ?? ""))
        {
            var userId = int.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture);
            var name = match.Groups["name"].Value.Trim();
            var flagsRaw = match.Groups["flags"].Value.Trim();
            var flags = ParseFlags(flagsRaw);
            var suffix = match.Groups["suffix"].Value;
            var isCurrent = currentUserId == userId
                || suffix.Contains("current", StringComparison.OrdinalIgnoreCase);
            users.Add(new AndroidUserProfile(
                UserId: userId,
                Name: string.IsNullOrWhiteSpace(name) ? $"User {userId}" : name,
                FlagsRaw: flagsRaw,
                IsRunning: suffix.Contains("running", StringComparison.OrdinalIgnoreCase),
                IsCurrent: isCurrent,
                IsManagedProfile: (flags & FlagManagedProfile) != 0
                    || name.Contains("work", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("managed", StringComparison.OrdinalIgnoreCase),
                IsGuest: (flags & FlagGuest) != 0
                    || name.Contains("guest", StringComparison.OrdinalIgnoreCase),
                IsRestricted: (flags & FlagRestricted) != 0
                    || name.Contains("restricted", StringComparison.OrdinalIgnoreCase),
                IsSystem: (flags & FlagSystem) != 0));
        }

        return users;
    }

    public static int? ParseCurrentUserId(string output)
    {
        var trimmed = (output ?? "").Trim();
        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    private static int ParseFlags(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;
        var normalized = raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? raw[2..] : raw;
        return int.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flags) ? flags : 0;
    }

    [GeneratedRegex(@"UserInfo\{(?<id>\d+):(?<name>[^:}]*):(?<flags>[0-9a-fA-Fx]+)\}(?<suffix>[^\r\n]*)", RegexOptions.Compiled)]
    private static partial Regex UserInfoRegex();
}
