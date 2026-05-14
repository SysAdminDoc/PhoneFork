using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.App.ViewModels;

public partial class MediaViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly ILogger _log;
    private MediaPlan? _lastPlan;

    public ObservableCollection<MediaCategoryRowViewModel> Rows { get; }

    [ObservableProperty] private string _status = "Assign Source and Destination, then click Scan.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _delete;
    [ObservableProperty] private bool _updateOnly = true;
    [ObservableProperty] private bool _preserveConflicts = true;
    [ObservableProperty] private bool _dryRun;
    [ObservableProperty] private double _totalMib;
    [ObservableProperty] private int _totalFiles;
    [ObservableProperty] private int _totalConflicts;
    [ObservableProperty] private string _currentFile = "";
    [ObservableProperty] private double _progressPercent;

    public MediaViewModel(DeviceService devices, AdbHostService host, ILogger log)
    {
        _devices = devices;
        _host = host;
        _log = log;
        Rows = new ObservableCollection<MediaCategoryRowViewModel>(
            Enum.GetValues<MediaCategory>().Select(c => new MediaCategoryRowViewModel(c, selected: true)));
        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(() =>
        {
            ScanCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanScan() => !IsBusy
        && _devices.RoleHolder(DeviceRole.Source) is not null
        && _devices.RoleHolder(DeviceRole.Destination) is not null;

    private bool CanApply() => !IsBusy && _lastPlan is not null && _lastPlan.TotalFilesToTransfer > 0;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync(CancellationToken ct)
    {
        var srcPhone = _devices.RoleHolder(DeviceRole.Source);
        var dstPhone = _devices.RoleHolder(DeviceRole.Destination);
        if (srcPhone is null || dstPhone is null) return;
        var srcData = _host.GetDevices().FirstOrDefault(d => d.Serial == srcPhone.Serial);
        var dstData = _host.GetDevices().FirstOrDefault(d => d.Serial == dstPhone.Serial);
        if (srcData is null || dstData is null) { Status = "Device disconnected."; return; }

        var picked = Rows.Where(r => r.IsSelected).Select(r => r.Category).ToList();
        if (picked.Count == 0) { Status = "Pick at least one category."; return; }

        IsBusy = true;
        try
        {
            Status = "Building manifests…";
            var svc = new MediaManifestService(_host.Client, _log);
            var srcManifest = await svc.BuildAsync(srcData, picked, ct: ct);
            var dstManifest = await svc.BuildAsync(dstData, picked, ct: ct);
            var plan = MediaDiffer.Build(srcManifest, dstManifest);
            _lastPlan = plan;

            var byCat = plan.CategoryDiffs.ToDictionary(d => d.Category);
            var srcByCat = srcManifest.Categories.ToDictionary(c => c.Category);
            var dstByCat = dstManifest.Categories.ToDictionary(c => c.Category);
            foreach (var row in Rows)
            {
                if (!row.IsSelected) continue;
                srcByCat.TryGetValue(row.Category, out var sm);
                dstByCat.TryGetValue(row.Category, out var dm);
                row.SrcFiles = sm?.FileCount ?? 0;
                row.SrcMib = (sm?.TotalSizeBytes ?? 0) / 1024.0 / 1024.0;
                row.DstFiles = dm?.FileCount ?? 0;
                row.DstMib = (dm?.TotalSizeBytes ?? 0) / 1024.0 / 1024.0;
                if (byCat.TryGetValue(row.Category, out var diff))
                {
                    row.PlanNew = diff.Count(MediaDiffOutcome.NewOnSource);
                    row.PlanConflicts = diff.Count(MediaDiffOutcome.Conflict);
                    row.PlanIdentical = diff.Count(MediaDiffOutcome.Identical);
                    row.PlanOnlyOnDst = diff.Count(MediaDiffOutcome.NewOnDest);
                    row.PlanMib = diff.BytesToTransfer / 1024.0 / 1024.0;
                }
            }
            TotalFiles = plan.TotalFilesToTransfer;
            TotalMib = plan.TotalBytesToTransfer / 1024.0 / 1024.0;
            TotalConflicts = plan.TotalConflicts;
            Status = $"Plan ready: {TotalFiles} files, {TotalMib:F1} MiB, {TotalConflicts} conflicts.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Media scan failed");
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
        if (_lastPlan is null) return;
        var srcPhone = _devices.RoleHolder(DeviceRole.Source);
        var dstPhone = _devices.RoleHolder(DeviceRole.Destination);
        if (srcPhone is null || dstPhone is null) return;
        var srcData = _host.GetDevices().FirstOrDefault(d => d.Serial == srcPhone.Serial);
        var dstData = _host.GetDevices().FirstOrDefault(d => d.Serial == dstPhone.Serial);
        if (srcData is null || dstData is null) { Status = "Device disconnected."; return; }

        IsBusy = true;
        try
        {
            var svc = new MediaSyncService(_host.Client, _log);
            var options = new MediaSyncOptions
            {
                Delete = Delete,
                UpdateOnly = UpdateOnly,
                PreserveConflicts = PreserveConflicts,
                DryRun = DryRun,
            };

            var prog = new Progress<MediaSyncProgress>(p =>
            {
                CurrentFile = p.CurrentRelPath;
                ProgressPercent = p.FilesTotal == 0 ? 0 : 100.0 * p.FilesDone / p.FilesTotal;
            });
            var result = await svc.ApplyAsync(srcData, dstData, _lastPlan, options, prog, ct);
            Status = DryRun
                ? $"Dry-run done — plan was {TotalFiles} files / {TotalMib:F1} MiB."
                : $"Pulled {result.FilesPulled}, pushed {result.FilesPushed}, skipped {result.FilesSkipped}, renamed {result.FilesRenamedAsConflict}, deleted {result.FilesDeleted}, errors {result.Errors} in {result.Elapsed.TotalSeconds:F1}s.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Media apply failed");
            Status = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll() { foreach (var r in Rows) r.IsSelected = true; }

    [RelayCommand]
    private void SelectNone() { foreach (var r in Rows) r.IsSelected = false; }
}
