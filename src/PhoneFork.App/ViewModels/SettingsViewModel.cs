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

public partial class SettingsViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly ILogger _log;

    public ObservableCollection<SettingsRowViewModel> Rows { get; } = new();
    public ICollectionView FilteredRows { get; }

    [ObservableProperty] private string _status = "Assign Source and Destination, then click Scan.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _showOnlyApplicable = true;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private int _totalDifferent;
    [ObservableProperty] private int _totalOnlyOnSource;
    [ObservableProperty] private int _totalSelected;
    [ObservableProperty] private bool _dryRun;
    [ObservableProperty] private bool _hasRows;

    public SettingsViewModel(DeviceService devices, AdbHostService host, ILogger log)
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

    partial void OnShowOnlyApplicableChanged(bool value) => FilteredRows.Refresh();
    partial void OnFilterChanged(string value) => FilteredRows.Refresh();

    private bool FilterPredicate(object o)
    {
        if (o is not SettingsRowViewModel row) return false;
        if (ShowOnlyApplicable && row.Outcome != SettingsDiffOutcome.Different && row.Outcome != SettingsDiffOutcome.OnlyOnSource)
            return false;
        if (!string.IsNullOrEmpty(Filter)
            && !row.Key.Contains(Filter, StringComparison.OrdinalIgnoreCase)
            && !row.NamespaceText.Contains(Filter, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private bool CanScan() => !IsBusy
        && _devices.RoleHolder(DeviceRole.Source) is not null
        && _devices.RoleHolder(DeviceRole.Destination) is not null;

    private bool CanApply() => !IsBusy && Rows.Any(r => r.IsSelected);

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync(CancellationToken ct)
    {
        var srcPhone = _devices.RoleHolder(DeviceRole.Source);
        var dstPhone = _devices.RoleHolder(DeviceRole.Destination);
        if (srcPhone is null || dstPhone is null) return;
        var srcData = _host.GetDevices().FirstOrDefault(d => d.Serial == srcPhone.Serial);
        var dstData = _host.GetDevices().FirstOrDefault(d => d.Serial == dstPhone.Serial);
        if (srcData is null || dstData is null) { Status = "Device disconnected."; return; }

        IsBusy = true;
        try
        {
            Status = "Capturing settings…";
            var svc = new SettingsSnapshotService(_host.Client, _log);
            var srcSnap = await svc.CaptureAsync(srcData, ct: ct);
            var dstSnap = await svc.CaptureAsync(dstData, ct: ct);
            var plan = SettingsDiffer.Build(srcSnap, dstSnap);

            Rows.Clear();
            HasRows = false;
            foreach (var nd in plan.Namespaces)
                foreach (var e in nd.Entries)
                    AddRow(new SettingsRowViewModel(e));
            FilteredRows.Refresh();
            TotalDifferent = Rows.Count(r => r.Outcome == SettingsDiffOutcome.Different);
            TotalOnlyOnSource = Rows.Count(r => r.Outcome == SettingsDiffOutcome.OnlyOnSource);
            RefreshSelectionState();
            Status = $"Plan ready: {TotalDifferent} different + {TotalOnlyOnSource} only-on-source. {plan.Namespaces.Sum(n => n.Count(SettingsDiffOutcome.Same))} keys already aligned.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Settings scan failed");
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ScanCommand.NotifyCanExecuteChanged();
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
            var picked = Rows.Where(r => r.IsSelected)
                .Select(r => new SettingsDiffEntry(r.Namespace, r.Key, r.Outcome, r.SourceValue, r.DestValue))
                .ToList();
            Status = $"Applying {picked.Count} key(s)…";
            var apply = new SettingsApplyService(_host.Client, _log);
            var result = await apply.ApplyAsync(dstData, picked, DryRun,
                new Progress<string>(_ => { }), ct);
            Status = DryRun
                ? $"Dry-run: would apply {result.Applied}, skipped {result.Skipped} locked/dangerous."
                : $"Applied {result.Applied}, skipped {result.Skipped}, failed {result.Failed} in {result.Elapsed.TotalSeconds:F1}s.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Settings apply failed");
            Status = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var r in Rows) if (r.Outcome != SettingsDiffOutcome.Same && r.Outcome != SettingsDiffOutcome.OnlyOnDest) r.IsSelected = true;
        RefreshSelectionState();
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var r in Rows) r.IsSelected = false;
        RefreshSelectionState();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private void AddRow(SettingsRowViewModel row)
    {
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsRowViewModel.IsSelected))
                RefreshSelectionState();
        };
        Rows.Add(row);
        HasRows = true;
    }

    private void RefreshSelectionState()
    {
        HasRows = Rows.Count > 0;
        TotalSelected = Rows.Count(r => r.IsSelected);
        ApplyCommand.NotifyCanExecuteChanged();
    }
}
