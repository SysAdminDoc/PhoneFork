using PhoneFork.Core.Models;

namespace PhoneFork.Core.Services;

public enum SettingsSafetyStatus
{
    Safe,
    Review,
    Blocked,
    Unknown,
}

public sealed record SamsungSettingRule(
    SettingsNamespace? Namespace,
    string KeyPattern,
    bool IsPrefix,
    SettingsSafetyStatus Status,
    string Category,
    string Rationale,
    string SourceId)
{
    public bool Matches(SettingsNamespace ns, string key)
    {
        if (Namespace is { } ruleNs && ruleNs != ns)
            return false;

        return IsPrefix
            ? key.StartsWith(KeyPattern, StringComparison.Ordinal)
            : string.Equals(KeyPattern, key, StringComparison.Ordinal);
    }
}

public sealed record SettingsSafetyAssessment(
    SettingsDiffEntry Entry,
    SettingsSafetyStatus Status,
    string Category,
    string Rationale,
    string SourceId);

public sealed record SettingsSafetySummary(
    int Safe,
    int Review,
    int Blocked,
    int Unknown)
{
    public int Total => Safe + Review + Blocked + Unknown;
}

public static class SamsungSettingsCorpus
{
    public static readonly IReadOnlyList<SamsungSettingRule> Rules = new[]
    {
        Safe(SettingsNamespace.System, "font_scale", "Display", "Font scale is a user-visible display preference.", "L13"),
        Safe(SettingsNamespace.System, "screen_brightness", "Display", "Manual brightness level is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.System, "screen_brightness_mode", "Display", "Adaptive/manual brightness mode is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.System, "screen_off_timeout", "Display", "Screen timeout is a user-visible display preference.", "L13"),
        Safe(SettingsNamespace.System, "accelerometer_rotation", "Display", "Auto-rotate preference is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.System, "user_rotation", "Display", "Rotation preference is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.System, "haptic_feedback_enabled", "Sound", "Touch feedback is a user-visible sound/vibration preference.", "L13"),
        Safe(SettingsNamespace.System, "sound_effects_enabled", "Sound", "Touch sound preference is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.System, "lockscreen_sounds_enabled", "Sound", "Lockscreen sound preference is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.System, "dtmf_tone", "Sound", "Dial-pad tone preference is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.System, "vibrate_when_ringing", "Sound", "Ring vibration preference is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.System, "ringtone", "Sound", "Default ringtone URI is already handled by PhoneFork media/settings flows.", "L13"),
        Safe(SettingsNamespace.System, "notification_sound", "Sound", "Default notification URI is already handled by PhoneFork media/settings flows.", "L13"),
        Safe(SettingsNamespace.System, "alarm_alert", "Sound", "Default alarm URI is already handled by PhoneFork media/settings flows.", "L13"),
        Safe(SettingsNamespace.System, "time_12_24", "Locale", "12/24-hour clock preference is reversible from Settings.", "L13"),

        Safe(SettingsNamespace.Secure, "lock_screen_show_notifications", "Notifications", "Lock-screen notification visibility is a user-visible preference.", "L13"),
        Safe(SettingsNamespace.Secure, "lock_screen_allow_private_notifications", "Notifications", "Private lock-screen notification visibility is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.Secure, "notification_badging", "Notifications", "Badge display is a user-visible launcher/notification preference.", "L13"),
        Safe(SettingsNamespace.Secure, "show_notification_snooze", "Notifications", "Notification snooze control is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.Secure, "accessibility_display_daltonizer", "Accessibility display", "Color correction mode is a user-visible display preference.", "L13"),
        Safe(SettingsNamespace.Secure, "accessibility_display_daltonizer_enabled", "Accessibility display", "Color correction toggle is a user-visible display preference.", "L13"),
        Safe(SettingsNamespace.Secure, "accessibility_display_inversion_enabled", "Accessibility display", "Color inversion toggle is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.Secure, "night_display_activated", "Display", "Night display toggle is a user-visible display preference.", "L13"),
        Safe(SettingsNamespace.Secure, "night_display_auto_mode", "Display", "Night display schedule mode is reversible from Settings.", "L13"),
        Safe(SettingsNamespace.Secure, "night_display_color_temperature", "Display", "Night display warmth is a user-visible display preference.", "L13"),

        Safe(null, "aod_", isPrefix: true, "Samsung Always On Display", "AOD settings are user-visible Samsung display preferences highlighted by community migration gaps.", "L21"),
        Safe(null, "edge_panel_", isPrefix: true, "Samsung Edge Panels", "Edge panel handle/layout settings are visible Samsung preferences highlighted by community migration gaps.", "L21"),
        Safe(null, "edge_handle_", isPrefix: true, "Samsung Edge Panels", "Edge handle settings are visible Samsung preferences highlighted by community migration gaps.", "L21"),
        Safe(null, "blue_light_filter", "Samsung display", "Samsung blue-light filter state is a user-visible display preference.", "L13"),
        Safe(null, "blue_light_filter_", isPrefix: true, "Samsung display", "Samsung blue-light filter settings are user-visible display preferences.", "L13"),
        Safe(null, "screen_mode_setting", "Samsung display", "Samsung screen mode is a user-visible display preference.", "L13"),
        Safe(null, "screen_mode_automatic_setting", "Samsung display", "Samsung automatic screen mode is a user-visible display preference.", "L13"),
        Safe(null, "display_night_theme", "Samsung display", "Dark-mode theme state is a user-visible display preference.", "L13"),
        Safe(null, "navigation_bar_gesture_while_hidden", "Samsung navigation", "Gesture navigation preference is reversible from Settings.", "L13"),
        Safe(null, "navigation_bar_button_to_hide_keyboard", "Samsung navigation", "Keyboard-hide button preference is reversible from Settings.", "L13"),
        Safe(null, "navigationbar_hide_bar_enabled", "Samsung navigation", "Navigation bar visibility preference is reversible from Settings.", "L13"),
        Safe(null, "navigationbar_key_order", "Samsung navigation", "Navigation button order is a user-visible preference.", "L13"),

        Review(SettingsNamespace.Global, "window_animation_scale", "Developer/display", "Animation scale is user-visible but can affect accessibility and perceived performance.", "L13"),
        Review(SettingsNamespace.Global, "transition_animation_scale", "Developer/display", "Animation scale is user-visible but can affect accessibility and perceived performance.", "L13"),
        Review(SettingsNamespace.Global, "animator_duration_scale", "Developer/display", "Animation scale is user-visible but can affect accessibility and perceived performance.", "L13"),
        Review(null, "volume_", isPrefix: true, "Sound", "Volume keys can be migrated, but device route and hearing-safety state should be reviewed first.", "L13"),

        Blocked(null, "enabled_accessibility_services", "Sensitive services", "Accessibility service grants are privileged trust decisions and must not be copied blindly.", "L21"),
        Blocked(null, "enabled_notification_listeners", "Sensitive services", "Notification listener grants expose private notifications and must not be copied blindly.", "L21"),
        Blocked(null, "accessibility_button_targets", "Sensitive services", "Accessibility service targets can grant broad app control and need a dedicated flow.", "L21"),
        Blocked(null, "lock_pattern_", isPrefix: true, "Lock credentials", "Lock credential state is device-bound and must not be cloned.", "L13"),
        Blocked(null, "lockscreen.password_", isPrefix: true, "Lock credentials", "Lock credential state is device-bound and must not be cloned.", "L13"),
        Blocked(null, "biometric_", isPrefix: true, "Biometrics", "Biometric enrollment and policy state is hardware-bound.", "L13"),
        Blocked(null, "fingerprint_", isPrefix: true, "Biometrics", "Fingerprint enrollment state is hardware-bound.", "L13"),
        Blocked(null, "face_", isPrefix: true, "Biometrics", "Face enrollment state is hardware-bound.", "L13"),
        Blocked(null, "knox_", isPrefix: true, "Samsung Knox", "Knox state is enterprise/OEM-bound and outside PhoneFork's no-root scope.", "L21"),
        Blocked(null, "secure_folder_", isPrefix: true, "Samsung Knox", "Secure Folder state is Knox-bound and must be handled by Samsung flows.", "L21"),
        Blocked(null, "payment_", isPrefix: true, "Wallet/payment", "Wallet/payment state is account-bound and must not be copied by settings put.", "S09"),
        Blocked(null, "nfc_payment_", isPrefix: true, "Wallet/payment", "Default payment state is account-bound and should be reconfigured by the wallet app.", "S09"),
    };

    public static SettingsSafetyAssessment Assess(SettingsDiffEntry entry)
    {
        if (SettingsApplyService.KnownLockedOrDangerous.Contains(entry.Key))
        {
            return new SettingsSafetyAssessment(
                entry,
                SettingsSafetyStatus.Blocked,
                "PhoneFork blocklist",
                "Known locked, device-specific, carrier-provisioned, or dangerous setting.",
                "L13");
        }

        var rule = Rules.FirstOrDefault(r => r.Matches(entry.Namespace, entry.Key));
        if (rule is null)
        {
            return new SettingsSafetyAssessment(
                entry,
                SettingsSafetyStatus.Unknown,
                "Uncatalogued",
                "No source-backed Samsung/Android safety rule exists yet for this key.",
                "L13");
        }

        return new SettingsSafetyAssessment(
            entry,
            rule.Status,
            rule.Category,
            rule.Rationale,
            rule.SourceId);
    }

    public static IReadOnlyList<SettingsSafetyAssessment> Assess(SettingsPlan plan)
        => plan.Namespaces
            .SelectMany(ns => ns.Entries)
            .Where(e => e.Outcome is SettingsDiffOutcome.Different or SettingsDiffOutcome.OnlyOnSource)
            .Select(Assess)
            .ToList();

    public static SettingsSafetySummary Summarize(IEnumerable<SettingsSafetyAssessment> assessments)
    {
        var list = assessments.ToList();
        return new SettingsSafetySummary(
            Safe: list.Count(a => a.Status == SettingsSafetyStatus.Safe),
            Review: list.Count(a => a.Status == SettingsSafetyStatus.Review),
            Blocked: list.Count(a => a.Status == SettingsSafetyStatus.Blocked),
            Unknown: list.Count(a => a.Status == SettingsSafetyStatus.Unknown));
    }

    public static bool CanApplyByDefault(SettingsDiffEntry entry)
        => Assess(entry).Status == SettingsSafetyStatus.Safe;

    public static bool CanApplyWithExplicitOverride(SettingsDiffEntry entry)
    {
        var status = Assess(entry).Status;
        return status is SettingsSafetyStatus.Safe or SettingsSafetyStatus.Review or SettingsSafetyStatus.Unknown;
    }

    private static SamsungSettingRule Safe(SettingsNamespace? ns, string key, string category, string rationale, string sourceId)
        => new(ns, key, IsPrefix: false, SettingsSafetyStatus.Safe, category, rationale, sourceId);

    private static SamsungSettingRule Safe(SettingsNamespace? ns, string key, bool isPrefix, string category, string rationale, string sourceId)
        => new(ns, key, isPrefix, SettingsSafetyStatus.Safe, category, rationale, sourceId);

    private static SamsungSettingRule Review(SettingsNamespace? ns, string key, string category, string rationale, string sourceId)
        => new(ns, key, IsPrefix: false, SettingsSafetyStatus.Review, category, rationale, sourceId);

    private static SamsungSettingRule Review(SettingsNamespace? ns, string key, bool isPrefix, string category, string rationale, string sourceId)
        => new(ns, key, isPrefix, SettingsSafetyStatus.Review, category, rationale, sourceId);

    private static SamsungSettingRule Blocked(SettingsNamespace? ns, string key, string category, string rationale, string sourceId)
        => new(ns, key, IsPrefix: false, SettingsSafetyStatus.Blocked, category, rationale, sourceId);

    private static SamsungSettingRule Blocked(SettingsNamespace? ns, string key, bool isPrefix, string category, string rationale, string sourceId)
        => new(ns, key, isPrefix, SettingsSafetyStatus.Blocked, category, rationale, sourceId);
}
