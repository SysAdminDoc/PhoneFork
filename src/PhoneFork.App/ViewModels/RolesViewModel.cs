using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.App.ViewModels;

public partial class RolesViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly ILogger _log;

    public ObservableCollection<RoleRowViewModel> Rows { get; } = new();

    [ObservableProperty] private string _status = "Assign Source + Destination, then click Scan.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _dryRun;

    public RolesViewModel(DeviceService devices, AdbHostService host, ILogger log)
    {
        _devices = devices;
        _host = host;
        _log = log;
        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(() =>
        {
            ScanCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanScan() => !IsBusy
        && _devices.RoleHolder(DeviceRole.Source) is not null
        && _devices.RoleHolder(DeviceRole.Destination) is not null;

    private bool CanApply() => !IsBusy && Rows.Any(r => r.IsSelected && !string.IsNullOrEmpty(r.SourceHolder));

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
            Status = "Snapshotting role holders…";
            var svc = new RoleService(_host.Client, _log);
            var src = await svc.SnapshotAsync(srcData, ct: ct);
            var dst = await svc.SnapshotAsync(dstData, ct: ct);
            var dstByRole = dst.Holders.ToDictionary(h => h.Role);

            Rows.Clear();
            foreach (var sh in src.Holders)
            {
                dstByRole.TryGetValue(sh.Role, out var dh);
                Rows.Add(new RoleRowViewModel(sh.Role, sh.HolderPackage, dh?.HolderPackage));
            }
            Status = $"{Rows.Count} roles inspected; {Rows.Count(r => r.IsSelected)} default-selected for apply.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Role scan failed");
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
            var queue = Rows.Where(r => r.IsSelected && !string.IsNullOrEmpty(r.SourceHolder))
                            .Select(r => (r.Role, r.SourceHolder))
                            .ToList();
            Status = $"Applying {queue.Count} role assignment(s)…";
            var svc = new RoleService(_host.Client, _log);
            var result = await svc.ApplyAsync(dstData, queue, DryRun,
                new Progress<string>(_ => { }), ct);
            foreach (var row in Rows.Where(r => r.IsSelected))
            {
                var match = result.Failures.FirstOrDefault(f => f.Role == row.Role);
                row.Status = match.Error is null ? (DryRun ? "would assign" : "assigned") : $"failed: {match.Error}";
            }
            Status = DryRun
                ? $"Dry-run: would assign {result.Applied}, would fail {result.Failed}."
                : $"Assigned {result.Applied}, failed {result.Failed}.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Role apply failed");
            Status = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAllDifferent()
    {
        foreach (var r in Rows)
            r.IsSelected = !string.IsNullOrEmpty(r.SourceHolder)
                           && !string.Equals(r.SourceHolder, r.DestHolder, StringComparison.Ordinal);
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var r in Rows) r.IsSelected = false;
    }
}
