namespace PhoneFork.Core.Models;

/// <summary>
/// Default-app roles PhoneFork enumerates and migrates. AOSP <c>RoleManager.ROLE_*</c> constants
/// surface through <c>cmd role get-role-holders &lt;role&gt;</c> from shell UID on Android 11+.
/// </summary>
public static class DefaultRoles
{
    public const string Dialer            = "android.app.role.DIALER";
    public const string Sms               = "android.app.role.SMS";
    public const string Browser           = "android.app.role.BROWSER";
    public const string Home              = "android.app.role.HOME";
    public const string Assistant         = "android.app.role.ASSISTANT";
    public const string CallRedirection   = "android.app.role.CALL_REDIRECTION";
    public const string CallScreening     = "android.app.role.CALL_SCREENING";
    public const string Emergency         = "android.app.role.EMERGENCY";

    /// <summary>Roles surfaced by PhoneFork's UI in this order.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Dialer, Sms, Browser, Home, Assistant, CallRedirection, CallScreening, Emergency,
    };

    public static string ShortLabel(string role) => role switch
    {
        Dialer          => "Dialer",
        Sms             => "SMS",
        Browser         => "Browser",
        Home            => "Home (launcher)",
        Assistant       => "Assistant",
        CallRedirection => "Call redirection",
        CallScreening   => "Call screening",
        Emergency       => "Emergency",
        _               => role,
    };
}

public sealed record RoleHolder(string Role, string? HolderPackage);

public sealed record RoleSnapshot
{
    public required string DeviceSerial { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required IReadOnlyList<RoleHolder> Holders { get; init; }
}
