using CommunityToolkit.Mvvm.ComponentModel;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

namespace PhoneFork.App.ViewModels;

public partial class DebloatRowViewModel : ObservableObject
{
    public DebloatEntry Entry { get; }
    public bool IsEnabledOnDevice { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _status = "";

    public DebloatRowViewModel(DebloatCandidate candidate, bool defaultSelected)
    {
        Entry = candidate.Entry;
        IsEnabledOnDevice = candidate.IsEnabled;
        _isSelected = defaultSelected && candidate.IsEnabled;
    }

    public string PackageId => Entry.PackageId;
    public string Label => Entry.DisplayLabel;
    public string Tier => Entry.Tier.ToString();
    public string List => Entry.List.ToString();
    public string OnDevice => IsEnabledOnDevice ? "Enabled" : "Disabled";
    public string Warning => Entry.Warning ?? "";
    public string Description => Entry.Description ?? "";
}
