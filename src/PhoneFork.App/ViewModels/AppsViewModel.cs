using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.App.ViewModels;

public partial class AppsViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly ILogger _log;

    public ObservableCollection<AppRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _status = "Assign Source and Destination, then click Scan.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private bool _reinstall;
    [ObservableProperty] private bool _dryRun;
    [ObservableProperty] private bool _hasRows;

    public AppsViewModel(DeviceService devices, AdbHostService host, ILogger log)
    {
        _devices = devices;
        _host = host;
        _log = log;
        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(() =>
        {
            ScanCommand.NotifyCanExecuteChanged();
            MigrateCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanScan() => _devices.RoleHolder(DeviceRole.Source) is not null && !IsBusy;
    private bool CanMigrate() => _devices.RoleHolder(DeviceRole.Source) is not null
                                 && _devices.RoleHolder(DeviceRole.Destination) is not null
                                 && Rows.Any(r => r.IsSelected)
                                 && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync(CancellationToken ct)
    {
        var src = _devices.RoleHolder(DeviceRole.Source);
        if (src is null) return;

        IsBusy = true;
        Status = $"Enumerating user apps on {src.DisplayName}…";
        Rows.Clear();
        HasRows = false;
        try
        {
            var deviceData = _host.GetDevices().FirstOrDefault(d => d.Serial == src.Serial);
            if (deviceData is null) { Status = "Source device disconnected."; return; }
            var catalog = new AppCatalogService(_host.Client, _log);
            var apps = await catalog.EnumerateUserAppsAsync(deviceData, ct);
            foreach (var a in apps.OrderBy(a => a.PackageName))
                AddRow(new AppRowViewModel(a));
            RefreshSelectionState();
            Status = $"Found {Rows.Count} user apps on {src.DisplayName}.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Scan failed");
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ScanCommand.NotifyCanExecuteChanged();
            MigrateCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMigrate))]
    private async Task MigrateAsync(CancellationToken ct)
    {
        var src = _devices.RoleHolder(DeviceRole.Source);
        var dst = _devices.RoleHolder(DeviceRole.Destination);
        if (src is null || dst is null) return;

        IsBusy = true;
        try
        {
            var srcData = _host.GetDevices().FirstOrDefault(d => d.Serial == src.Serial);
            var dstData = _host.GetDevices().FirstOrDefault(d => d.Serial == dst.Serial);
            if (srcData is null || dstData is null) { Status = "Device disconnected."; return; }

            var installer = new AppInstallerService(_host.Client, _log);
            var picked = Rows.Where(r => r.IsSelected).ToList();
            int ok = 0, fail = 0;

            foreach (var row in picked)
            {
                ct.ThrowIfCancellationRequested();
                row.Status = "Pulling…";
                var progress = new Progress<string>(s => row.Status = s);
                try
                {
                    if (DryRun)
                    {
                        await installer.PullApksToCacheAsync(srcData, row.App, progress, ct);
                        row.Status = "Dry-run pulled";
                        ok++;
                    }
                    else
                    {
                        var r = await installer.MigrateAsync(srcData, dstData, row.App, Reinstall, progress, ct);
                        row.Status = r.Success ? $"Migrated ({r.Duration.TotalSeconds:F1}s)" : $"Failed: {r.Error}";
                        if (r.Success) ok++; else fail++;
                    }
                }
                catch (Exception ex)
                {
                    row.Status = $"Failed: {ex.Message}";
                    fail++;
                }
            }

            Status = $"{ok} migrated, {fail} failed.";
        }
        finally
        {
            IsBusy = false;
            ScanCommand.NotifyCanExecuteChanged();
            MigrateCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void SelectAll() { foreach (var r in Rows) r.IsSelected = true; RefreshSelectionState(); }

    [RelayCommand]
    private void SelectNone() { foreach (var r in Rows) r.IsSelected = false; RefreshSelectionState(); }

    partial void OnIsBusyChanged(bool value)
    {
        ScanCommand.NotifyCanExecuteChanged();
        MigrateCommand.NotifyCanExecuteChanged();
    }

    private void AddRow(AppRowViewModel row)
    {
        row.PropertyChanged += RowOnPropertyChanged;
        Rows.Add(row);
        HasRows = true;
    }

    private void RowOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppRowViewModel.IsSelected))
            RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        HasRows = Rows.Count > 0;
        SelectedCount = Rows.Count(r => r.IsSelected);
        MigrateCommand.NotifyCanExecuteChanged();
    }
}
