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
    [ObservableProperty] private bool _hasRows;
    [ObservableProperty] private string _dependencyWarningLine = "";

    // Kept from the last scan so Apply can cross-check the queue against the dependency
    // graph without re-reading the device (F121).
    private DebloatDataset? _lastDataset;
    private IReadOnlyList<DebloatCandidate> _lastCandidates = Array.Empty<DebloatCandidate>();

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

    [RelayCommand(CanExecute = nameof(CanScan), IncludeCancelCommand = true)]
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
            // F102 - apply per-device overrides so One UI regressions (for example
            // UAD-NG #1394 smartsuggestions) move out of the default profiles before scanning.
            var dataset = await DebloatDatasetResolver.LoadForDeviceAsync(_host.Client, dstData, _log, ct: ct);
            Status = "Scanning device packages…";
            var scanner = new DebloatScanner(_host.Client, _log, dataset);
            var candidates = await scanner.ScanAsync(dstData, ct);

            _lastDataset = dataset;
            _lastCandidates = candidates;
            DependencyWarningLine = "";

            var defaultTiers = ProfileTiers(Profile);
            Rows.Clear();
            HasRows = false;
            foreach (var c in candidates.OrderBy(c => c.Entry.PackageId, StringComparer.Ordinal))
            {
                var defaultSelected = c.IsEnabled && defaultTiers.Contains(c.Entry.Tier);
                AddRow(new DebloatRowViewModel(c, defaultSelected));
            }
            FilteredRows.Refresh();
            RefreshSelectionState();
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
            ScanCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanApply), IncludeCancelCommand = true)]
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

            // F121 - warn about packages that something still enabled depends on, before writing.
            var dependencyWarnings = _lastDataset is null
                ? Array.Empty<DebloatDependencyWarning>()
                : DebloatDependencyCheck.Evaluate(_lastDataset, picked, _lastCandidates).ToArray();
            DependencyWarningLine = dependencyWarnings.Length == 0
                ? ""
                : $"{dependencyWarnings.Length} selected package(s) are needed by something that stays enabled: "
                  + string.Join("  ", dependencyWarnings.Select(w => w.Describe()));

            Status = $"Applying disable-user on {picked.Count} package(s)…";
            var svc = new DebloatService(_host.Client, _log);
            var result = await svc.ApplyAsync(dstData, picked, DryRun,
                new Progress<string>(_ => { }), ct);
            if (!DryRun)
                LastSnapshotPath = result.SnapshotPath;

            // Reflect new state in the rows.
            foreach (var r in Rows.Where(r => r.IsSelected))
            {
                var match = result.Results.FirstOrDefault(x => x.PackageId == r.PackageId);
                r.Status = match?.Success == true ? (DryRun ? "would disable" : "disabled") : $"failed: {match?.Output}";
            }
            var artifacts = string.IsNullOrWhiteSpace(result.SnapshotPath)
                ? Array.Empty<MigrationReceiptArtifact>()
                : new[] { new MigrationReceiptArtifact("rollback-snapshot", result.SnapshotPath) };
            var receiptPath = await new MigrationReceiptService(_log).WriteAsync(
                MigrationReceiptService.Create(
                    operation: "wpf-debloat-apply",
                    dryRun: DryRun,
                    devices: new[] { MigrationReceiptService.Device("destination", dstData) },
                    categories: new[]
                    {
                        MigrationReceiptService.Category(
                            "debloat",
                            planned: picked.Count,
                            succeeded: result.Disabled,
                            skipped: result.AlreadyDisabled,
                            failed: result.Failed,
                            failureDetails: result.Results.Where(r => !r.Success).Select(r => $"{r.PackageId}: {r.Output}"),
                            artifacts: artifacts),
                    },
                    artifacts: artifacts),
                ct);
            Status = DryRun
                ? $"Dry-run: would disable {result.Disabled}, already disabled {result.AlreadyDisabled}, would fail {result.Failed}. No changes written."
                : $"Disabled {result.Disabled}, already disabled {result.AlreadyDisabled}, failed {result.Failed} in {result.Elapsed.TotalSeconds:F1}s. Snapshot: {result.SnapshotPath}. Receipt: {receiptPath}";
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
        RefreshSelectionState();
        Status = $"{TotalSelected} selected by profile {Profile}.";
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var r in Rows) r.IsSelected = false;
        RefreshSelectionState();
    }

    partial void OnIsBusyChanged(bool value)
    {
        CancelRunningCommand.NotifyCanExecuteChanged();
        ScanCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private void AddRow(DebloatRowViewModel row)
    {
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DebloatRowViewModel.IsSelected))
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

    // Shared with the CLI so a tier upstream reclassifies as Unsafe drops out of both at once (F110).
    private static HashSet<DebloatTier> ProfileTiers(DebloatProfile profile)
        => DebloatProfiles.TiersFor(profile.ToString());

    /// <summary>
    /// Cancels whichever long-running command is in flight (F113). Scan and Apply are
    /// mutually exclusive because both are gated on <see cref="IsBusy"/>, so one control serves both.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelRunning()
    {
        if (ScanCancelCommand.CanExecute(null)) ScanCancelCommand.Execute(null);
        if (ApplyCancelCommand.CanExecute(null)) ApplyCancelCommand.Execute(null);
    }

    private bool CanCancel() => IsBusy;}
