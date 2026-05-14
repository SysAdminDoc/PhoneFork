using CommunityToolkit.Mvvm.ComponentModel;
using PhoneFork.Core.Models;

namespace PhoneFork.App.ViewModels;

public partial class AppRowViewModel : ObservableObject
{
    public AppInfo App { get; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private double _progress;

    public AppRowViewModel(AppInfo app)
    {
        App = app;
    }

    public string PackageName => App.PackageName;
    public string Label => App.SafeLabel;
    public string Version => string.IsNullOrEmpty(App.VersionName) ? "—" : App.VersionName;
    public int SplitCount => App.RemoteApkPaths.Count;
    public string SizeText => $"{App.TotalSizeBytes / 1024.0 / 1024.0:F1} MiB";
}
