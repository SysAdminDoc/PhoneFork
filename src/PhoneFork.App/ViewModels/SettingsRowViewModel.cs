using CommunityToolkit.Mvvm.ComponentModel;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.App.ViewModels;

public partial class SettingsRowViewModel : ObservableObject
{
    public SettingsNamespace Namespace { get; }
    public string Key { get; }
    public SettingsDiffOutcome Outcome { get; }
    public string SourceValue { get; }
    public string DestValue { get; }
    public SettingsSafetyStatus Safety { get; }
    public string SafetyCategory { get; }
    public string SafetyDetail { get; }

    [ObservableProperty] private bool _isSelected;

    public SettingsRowViewModel(SettingsDiffEntry entry)
    {
        Namespace = entry.Namespace;
        Key = entry.Key;
        Outcome = entry.Outcome;
        SourceValue = entry.SourceValue ?? "";
        DestValue = entry.DestValue ?? "";
        var assessment = SamsungSettingsCorpus.Assess(entry);
        Safety = assessment.Status;
        SafetyCategory = assessment.Category;
        SafetyDetail = assessment.Rationale;
        // Default-selected only for safe "Different" keys — source-only and uncatalogued keys are opt-in/CLI-only.
        _isSelected = entry.Outcome == SettingsDiffOutcome.Different && IsSafeToApply;
    }

    public bool IsSafeToApply => Safety == SettingsSafetyStatus.Safe;

    public string NamespaceText => Namespace.ToString().ToLowerInvariant();
    public string SafetyText => Safety.ToString().ToLowerInvariant();
    public string OutcomeText => Outcome switch
    {
        SettingsDiffOutcome.Different => "different",
        SettingsDiffOutcome.OnlyOnSource => "only src",
        SettingsDiffOutcome.OnlyOnDest => "only dst",
        SettingsDiffOutcome.Same => "same",
        _ => Outcome.ToString(),
    };
}
