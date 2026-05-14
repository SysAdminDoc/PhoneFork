using CommunityToolkit.Mvvm.ComponentModel;
using PhoneFork.Core.Models;

namespace PhoneFork.App.ViewModels;

public partial class RoleRowViewModel : ObservableObject
{
    public string Role { get; }
    public string SourceHolder { get; }
    public string DestHolder { get; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _status = "";

    public RoleRowViewModel(string role, string? sourceHolder, string? destHolder)
    {
        Role = role;
        SourceHolder = sourceHolder ?? "";
        DestHolder = destHolder ?? "";
        // Default-select only when source has a holder and it differs from dest.
        _isSelected = !string.IsNullOrEmpty(SourceHolder)
                      && !string.Equals(SourceHolder, DestHolder, StringComparison.Ordinal);
    }

    public string Label => DefaultRoles.ShortLabel(Role);

    public string MatchText =>
        string.Equals(SourceHolder, DestHolder, StringComparison.Ordinal)
            ? (string.IsNullOrEmpty(SourceHolder) ? "—" : "match")
            : "different";
}
