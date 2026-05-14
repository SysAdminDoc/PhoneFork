using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.App.ViewModels;

public enum DebloatProfile { Conservative, Recommended, Aggressive }

public partial class DebloatViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly ILogger _log;

    public ObservableCollection<DebloatRowViewModel> Rows { get; } = new();
    public ICollectionView FilteredRows { get; }
    public ObservableCollection<DebloatProfile> Profiles { get; } = new(Enum.GetValues<DebloatProfile>());

    [ObservableProperty] private string _status = "Assign Destination, then click Scan.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private bool _includeEnabled = true;
    [ObservableProperty] private bool _includeDisabled;
    [ObservableProperty] private bool _showDelete = true;
    [ObservableProperty] private bool _showReplace = true;
    [ObservableProperty] private bool _showCaution;
    [ObservableProperty] private bool _showUnsafe;
    [ObservableProperty] private bool _dryRun;
    [ObservableProperty] private DebloatProfile _profile = DebloatProfile.Conservative;
    [ObservableProperty] private int _totalSelected;
    [ObservableProperty] private string _lastSnapshotPath = "";

    public DebloatViewModel(DeviceService devices, AdbHostService host, ILogger log)
    {
        _devices = devices;
        _host = host;
        _log = log;
        FilteredRows = CollectionViewSource.GetDefaultView(Rows);
        FilteredRows.Filter = FilterPredicate;
        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(() =>
        {
            ScanCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged();
        });
    }

    partial void OnFilterChanged(string value) => FilteredRows.Refresh();
    partial void OnIncludeEnabledChanged(bool value) => FilteredRows.Refresh();
    partial void OnIncludeDisabledChanged(bool value) => FilteredRows.Refresh();
    partial void OnShowDeleteChanged(bool value) => FilteredRows.Refresh();
    partial void OnShowReplaceChanged(bool value) => FilteredRows.Refresh();
    partial void OnShowCautionChanged(bool value) => FilteredRows.Refresh();
    partial void OnShowUnsafeChanged(bool value) => FilteredRows.Refresh();

    private bool FilterPredicate(object o)
    {
        if (o is not DebloatRowViewModel row) return false;
        if (row.IsEnabledOnDevice && !IncludeEnabled) return false;
        if (!row.IsEnabledOnDevice && !IncludeDisabled) return false;
        var t = row.Entry.Tier;
        if (t == DebloatTier.Delete   && !ShowDelete)   return false;
        if (t == DebloatTier.Replace  && !ShowReplace)  return false;
        if (t == DebloatTier.Caution  && !ShowCaution)  return false;
        if (t == DebloatTier.Unsafe   && !ShowUnsafe)   return false;
        if (!string.IsNullOrEmpty(Filter)
            && !row.PackageId.Contains(Filter, StringComparison.OrdinalIgnoreCase)
            && !row.Label.Contains(Filter, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private bool CanScan() => !IsBusy && _devices.RoleHolder(DeviceRole.Destination) is not null;
    private bool CanApply() => !IsBusy && Rows.Any(r => r.IsSelected);

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync(CancellationToken ct)
    {
        var dstPhone = _devices.RoleHolder(DeviceRole.Destination);
        if (dstPhone is null) return;
        var dstData = _host.GetDevices().FirstOrDefault(d => d.Serial == dstPhone.Serial);
        if (dstData is null) { Status = "Destination disconnected."; return; }

        IsBusy = true;
        try
        {
            Status = "Loading dataset…";
            var dataset = DebloatDataset.Load(_log);
            Status = "Scanning device packages…";
            var scanner = new DebloatScanner(_host.Client, _log, dataset);
            var candidates = await scanner.ScanAsync(dstData, ct);

            var defaultTiers = ProfileTiers(Profile);
            Rows.Clear();
            foreach (var c in candidates.OrderBy(c => c.Entry.PackageId, StringComparer.Ordinal))
            {
                var defaultSelected = c.IsEnabled && defaultTiers.Contains(c.Entry.Tier);
                Rows.Add(new DebloatRowViewModel(c, defaultSelected));
            }
            FilteredRows.Refresh();
            TotalSelected = Rows.Count(r => r.IsSelected);
            Status = $"{Rows.Count} packages matched. {TotalSelected} selected (profile: {Profile}).";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Debloat scan failed");
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync(CancellationToken ct)
    {
        var dstPhone = _devices.RoleHolder(DeviceRole.Destination);
        if (dstPhone is null) return;
        var dstData = _host.GetDevices().FirstOrDefault(d => d.Serial == dstPhone.Serial);
        if (dstData is null) { Status = "Destination disconnected."; return; }

        IsBusy = true;
        try
        {
            var picked = Rows.Where(r => r.IsSelected).Select(r => r.PackageId).ToList();
            Status = $"Applying disable-user on {picked.Count} package(s)…";
            var svc = new DebloatService(_host.Client, _log);
            var result = await svc.ApplyAsync(dstData, picked, DryRun,
                new Progress<string>(_ => { }), ct);
            LastSnapshotPath = result.SnapshotPath;

            // Reflect new state in the rows.
            foreach (var r in Rows.Where(r => r.IsSelected))
            {
                var match = result.Results.FirstOrDefault(x => x.PackageId == r.PackageId);
                r.Status = match?.Success == true ? (DryRun ? "would disable" : "disabled") : $"failed: {match?.Output}";
            }
            Status = DryRun
                ? $"Dry-run: would disable {result.Disabled}, already disabled {result.AlreadyDisabled}, would fail {result.Failed}. Snapshot at {result.SnapshotPath}."
                : $"Disabled {result.Disabled}, already disabled {result.AlreadyDisabled}, failed {result.Failed} in {result.Elapsed.TotalSeconds:F1}s. Snapshot: {result.SnapshotPath}";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Debloat apply failed");
            Status = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ApplyProfile()
    {
        var tiers = ProfileTiers(Profile);
        foreach (var r in Rows) r.IsSelected = r.IsEnabledOnDevice && tiers.Contains(r.Entry.Tier);
        TotalSelected = Rows.Count(r => r.IsSelected);
        Status = $"{TotalSelected} selected by profile {Profile}.";
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var r in Rows) r.IsSelected = false;
        TotalSelected = 0;
    }

    private static HashSet<DebloatTier> ProfileTiers(DebloatProfile profile) => profile switch
    {
        DebloatProfile.Conservative => new HashSet<DebloatTier> { DebloatTier.Delete },
        DebloatProfile.Recommended  => new HashSet<DebloatTier> { DebloatTier.Delete, DebloatTier.Replace },
        DebloatProfile.Aggressive   => new HashSet<DebloatTier> { DebloatTier.Delete, DebloatTier.Replace, DebloatTier.Caution },
        _ => new HashSet<DebloatTier> { DebloatTier.Delete },
    };
}
