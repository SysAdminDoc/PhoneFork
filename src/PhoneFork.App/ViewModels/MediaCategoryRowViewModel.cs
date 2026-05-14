using CommunityToolkit.Mvvm.ComponentModel;
using PhoneFork.Core.Models;

namespace PhoneFork.App.ViewModels;

public partial class MediaCategoryRowViewModel : ObservableObject
{
    public MediaCategory Category { get; }

    [ObservableProperty] private bool _isSelected;

    /// <summary>Filled by Scan: source-side file count / total MiB.</summary>
    [ObservableProperty] private int _srcFiles;
    [ObservableProperty] private double _srcMib;

    /// <summary>Filled by Scan: destination-side file count / total MiB.</summary>
    [ObservableProperty] private int _dstFiles;
    [ObservableProperty] private double _dstMib;

    /// <summary>Filled by Plan: new-on-src / conflicts / identical / only-on-dst / MiB to transfer.</summary>
    [ObservableProperty] private int _planNew;
    [ObservableProperty] private int _planConflicts;
    [ObservableProperty] private int _planIdentical;
    [ObservableProperty] private int _planOnlyOnDst;
    [ObservableProperty] private double _planMib;

    [ObservableProperty] private string _status = "";

    public MediaCategoryRowViewModel(MediaCategory category, bool selected = true)
    {
        Category = category;
        _isSelected = selected;
    }

    public string Label => Category.Label();
    public string RemoteRoot => Category.RemotePath();
}
