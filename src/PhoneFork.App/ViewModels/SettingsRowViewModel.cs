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

    [ObservableProperty] private bool _isSelected;

    public SettingsRowViewModel(SettingsDiffEntry entry)
    {
        Namespace = entry.Namespace;
        Key = entry.Key;
        Outcome = entry.Outcome;
        SourceValue = entry.SourceValue ?? "";
        DestValue = entry.DestValue ?? "";
        // Default-selected only for "Different" — OnlyOnSource is opt-in by user.
        _isSelected = entry.Outcome == SettingsDiffOutcome.Different;
    }

    public string NamespaceText => Namespace.ToString().ToLowerInvariant();
    public string OutcomeText => Outcome switch
    {
        SettingsDiffOutcome.Different => "different",
        SettingsDiffOutcome.OnlyOnSource => "only src",
        SettingsDiffOutcome.OnlyOnDest => "only dst",
        SettingsDiffOutcome.Same => "same",
        _ => Outcome.ToString(),
    };
}
