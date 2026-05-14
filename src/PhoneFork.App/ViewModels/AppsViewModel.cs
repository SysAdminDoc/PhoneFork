using System.Collections.ObjectModel;
using System.IO;
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
        try
        {
            var deviceData = _host.GetDevices().FirstOrDefault(d => d.Serial == src.Serial);
            if (deviceData is null) { Status = "Source device disconnected."; return; }
            var catalog = new AppCatalogService(_host.Client, _log);
            var apps = await catalog.EnumerateUserAppsAsync(deviceData, ct);
            foreach (var a in apps.OrderBy(a => a.PackageName))
                Rows.Add(new AppRowViewModel(a));
            SelectedCount = Rows.Count(r => r.IsSelected);
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
        var srcData = _host.GetDevices().FirstOrDefault(d => d.Serial == src.Serial);
        var dstData = _host.GetDevices().FirstOrDefault(d => d.Serial == dst.Serial);
        if (srcData is null || dstData is null) { Status = "Device disconnected."; IsBusy = false; return; }

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
                    // Pull-only path: re-use the cache via installer (just don't call install).
                    var pkgCache = Path.Combine(installer.CacheRoot, srcData.Serial, row.App.PackageName);
                    Directory.CreateDirectory(pkgCache);
                    foreach (var remote in row.App.RemoteApkPaths)
                    {
                        using var sync = new AdvancedSharpAdbClient.SyncService(_host.Client, srcData);
                        using var fs = File.Create(Path.Combine(pkgCache, Path.GetFileName(remote)));
                        await sync.PullAsync(remote, fs, callback: null, useV2: false, cancellationToken: ct);
                    }
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
        IsBusy = false;
    }

    [RelayCommand]
    private void SelectAll() { foreach (var r in Rows) r.IsSelected = true; SelectedCount = Rows.Count; MigrateCommand.NotifyCanExecuteChanged(); }

    [RelayCommand]
    private void SelectNone() { foreach (var r in Rows) r.IsSelected = false; SelectedCount = 0; MigrateCommand.NotifyCanExecuteChanged(); }
}
