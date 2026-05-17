using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AdvancedSharpAdbClient.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.App.ViewModels;

public sealed class OperationStatusRowViewModel
{
    public OperationStatusRowViewModel(string area, string state, string detail, string severity = "Info")
    {
        Area = area;
        State = state;
        Detail = detail;
        Severity = severity;
        When = DateTimeOffset.Now;
    }

    public DateTimeOffset When { get; }
    public string Time => When.ToString("HH:mm:ss");
    public string Area { get; }
    public string State { get; }
    public string Detail { get; }
    public string Severity { get; }
}

public sealed class TrustedPairRowViewModel
{
    public TrustedPairRowViewModel(TrustedPair pair)
    {
        Label = pair.Label;
        SerialHash = pair.SerialHashValue;
        Transport = pair.Transport.ToString();
        LastSeen = pair.LastSeen.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        LastEndpoint = pair.LastEndpoint ?? "";
    }

    public string Label { get; }
    public string SerialHash { get; }
    public string Transport { get; }
    public string LastSeen { get; }
    public string LastEndpoint { get; }
}

public sealed class PreflightFindingRowViewModel
{
    public PreflightFindingRowViewModel(HonestyFinding finding)
    {
        Level = finding.Level.ToString();
        Title = finding.Title;
        PackageId = finding.PackageId ?? "";
        Detail = finding.Detail;
    }

    public string Level { get; }
    public string Title { get; }
    public string PackageId { get; }
    public string Detail { get; }
}

public partial class OperationsViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly SecurityPostureService _posture;
    private readonly TrustedPairRegistry _trusted;
    private readonly ILogger _log;
    private readonly AdbBurstModeService _burst;

    public ObservableCollection<OperationStatusRowViewModel> Rows { get; } = new();
    public ObservableCollection<TrustedPairRowViewModel> TrustedPairs { get; } = new();
    public ObservableCollection<PreflightFindingRowViewModel> PreflightFindings { get; } = new();

    [ObservableProperty] private string _status = "Use this panel for helper, pre-flight, Smart Switch, backup, trust, and verification operations.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _helperApkPath = ResolveDefaultHelperApk();
    [ObservableProperty] private string _backupPath = "";
    [ObservableProperty] private string _sourceLine = "Source not assigned.";
    [ObservableProperty] private string _destinationLine = "Destination not assigned.";
    [ObservableProperty] private string _smartSwitchLine = "Smart Switch not checked.";
    [ObservableProperty] private string _trustedLine = "Trusted pairs not loaded.";
    [ObservableProperty] private string _burstLine = "ADB Burst Mode not checked.";
    [ObservableProperty] private string _preflightLine = "Pre-flight not run.";
    [ObservableProperty] private string _messagesLine = "Messages transition not checked.";
    [ObservableProperty] private string _mediaIntegrityLine = "Media integrity not run.";

    public OperationsViewModel(DeviceService devices, AdbHostService host, SecurityPostureService posture, TrustedPairRegistry trusted, ILogger log)
    {
        _devices = devices;
        _host = host;
        _posture = posture;
        _trusted = trusted;
        _log = log.ForContext<OperationsViewModel>();
        _burst = new AdbBurstModeService(log);

        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(RefreshDeviceLines);
        RefreshOverview();
    }

    [RelayCommand]
    private void RefreshOverview()
    {
        RefreshDeviceLines();
        RefreshSmartSwitchLine();
        RefreshTrustedRows();
        RefreshBurstLine();
        AddRow("Overview", "Ready", "Operational surfaces refreshed.");
    }

    [RelayCommand]
    private void DetectSmartSwitch()
    {
        RefreshSmartSwitchLine();
        AddRow("Smart Switch", "Checked", SmartSwitchLine);
        Status = SmartSwitchLine;
    }

    [RelayCommand]
    private void ToggleBurst()
    {
        var enable = !_burst.IsBurstEnabled;
        var restartRequired = _burst.Set(enable);
        RefreshBurstLine();
        var suffix = restartRequired ? " Restart ADB for this to affect the server." : " No restart needed.";
        Status = BurstLine + suffix;
        AddRow("Burst mode", enable ? "Enabled" : "Disabled", Status);
    }

    [RelayCommand]
    private async Task InstallHelperSourceAsync(CancellationToken ct) =>
        await InstallHelperAsync(DeviceRole.Source, ct);

    [RelayCommand]
    private async Task InstallHelperDestinationAsync(CancellationToken ct) =>
        await InstallHelperAsync(DeviceRole.Destination, ct);

    [RelayCommand]
    private async Task ProbeHelperSourceAsync(CancellationToken ct) =>
        await ProbeHelperAsync(DeviceRole.Source, ct);

    [RelayCommand]
    private async Task ProbeHelperDestinationAsync(CancellationToken ct) =>
        await ProbeHelperAsync(DeviceRole.Destination, ct);

    [RelayCommand]
    private async Task UninstallHelperSourceAsync(CancellationToken ct) =>
        await UninstallHelperAsync(DeviceRole.Source, ct);

    [RelayCommand]
    private async Task UninstallHelperDestinationAsync(CancellationToken ct) =>
        await UninstallHelperAsync(DeviceRole.Destination, ct);

    [RelayCommand]
    private async Task CheckShizukuSourceAsync(CancellationToken ct) =>
        await CheckShizukuAsync(DeviceRole.Source, ct);

    [RelayCommand]
    private async Task CheckShizukuDestinationAsync(CancellationToken ct) =>
        await CheckShizukuAsync(DeviceRole.Destination, ct);

    [RelayCommand]
    private async Task RunPreflightAsync(CancellationToken ct)
    {
        if (!TryGetRoleDevice(DeviceRole.Source, out var source, out var sourceLabel) ||
            !TryGetRoleDevice(DeviceRole.Destination, out var destination, out var destinationLabel))
        {
            Status = "Assign authorized Source and Destination devices before running pre-flight.";
            AddRow("Pre-flight", "Blocked", Status, "Warning");
            return;
        }

        await RunBusyAsync("Running pre-flight bundle...", async token =>
        {
            var honesty = new SamsungHonestyService(_host.Client, _log);
            var svc = new PreflightService(_host.Client, _posture, honesty, _log);
            var report = await svc.RunAsync(source, destination, token);

            PreflightFindings.Clear();
            foreach (var finding in report.AllFindings)
                PreflightFindings.Add(new PreflightFindingRowViewModel(finding));
            MessagesLine = report.Messages.Summary;

            var csc = report.Csc is null
                ? "CSC unavailable"
                : $"CSC {report.Csc.SourceCsc}/{report.Csc.SourceCountry}/{report.Csc.SourceLocale} -> {report.Csc.DestinationCsc}/{report.Csc.DestinationCountry}/{report.Csc.DestinationLocale}";
            PreflightLine = $"{sourceLabel} -> {destinationLabel}: {report.BlockerCount} blockers, {report.WarningCount} warnings. {csc}. Source {report.SourcePosture.SummaryLine()}; destination {report.DestinationPosture.SummaryLine()}.";
            Status = PreflightLine;
            AddRow("Pre-flight", report.HasBlockers ? "Blockers" : "Complete", PreflightLine, report.HasBlockers ? "Blocker" : "Info");
            AddRow("Messages", report.Messages.CanUseHelperSms ? "Clear" : "Review", MessagesLine, report.Messages.CanUseHelperSms ? "Info" : "Warning");
        }, ct);
    }

    [RelayCommand]
    private async Task InspectBackupAsync(CancellationToken ct)
    {
        var path = BackupPath.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            Status = "Paste an AppManager backup directory, Open Android Backup directory, or .ab file path first.";
            AddRow("Backup interop", "Blocked", Status, "Warning");
            return;
        }

        await RunBusyAsync("Inspecting backup path...", async token =>
        {
            token.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                InspectBackupFile(path);
                return;
            }

            if (!Directory.Exists(path))
            {
                Status = $"Backup path does not exist: {path}";
                AddRow("Backup interop", "Missing", Status, "Warning");
                return;
            }

            await InspectBackupDirectoryAsync(path, token);
        }, ct);
    }

    [RelayCommand]
    private async Task VerifyMediaIntegrityAsync(CancellationToken ct)
    {
        if (!TryGetRoleDevice(DeviceRole.Source, out var source, out var sourceLabel) ||
            !TryGetRoleDevice(DeviceRole.Destination, out var destination, out var destinationLabel))
        {
            Status = "Assign authorized Source and Destination devices before verifying media.";
            AddRow("Media integrity", "Blocked", Status, "Warning");
            return;
        }

        await RunBusyAsync("Building media manifests for size/mtime verification...", async token =>
        {
            var categories = Enum.GetValues<MediaCategory>();
            var manifestSvc = new MediaManifestService(_host.Client, _log);
            var integrity = new MediaIntegrityService(_host.Client, _log);
            var sourceManifest = await manifestSvc.BuildAsync(source, categories, ct: token);
            var destinationManifest = await manifestSvc.BuildAsync(destination, categories, ct: token);

            var destinationByCategory = destinationManifest.Categories.ToDictionary(c => c.Category);
            var checkedFiles = 0;
            var matchedFiles = 0;
            var mismatches = 0;
            var missing = 0;

            foreach (var sourceCategory in sourceManifest.Categories)
            {
                destinationByCategory.TryGetValue(sourceCategory.Category, out var destinationCategory);
                var report = await integrity.VerifyAsync(
                    source,
                    destination,
                    sourceCategory.Files,
                    destinationCategory?.Files ?? Array.Empty<MediaFile>(),
                    MediaIntegrityMode.SizeAndMtime,
                    token);
                checkedFiles += report.FilesChecked;
                matchedFiles += report.FilesMatched;
                mismatches += report.Mismatches.Count;
                missing += report.MissingOnDestination.Count;
            }

            MediaIntegrityLine = $"{sourceLabel} -> {destinationLabel}: checked {checkedFiles}, matched {matchedFiles}, mismatches {mismatches}, missing {missing} using size/mtime.";
            Status = MediaIntegrityLine;
            AddRow("Media integrity", mismatches == 0 && missing == 0 ? "Clean" : "Review", MediaIntegrityLine, mismatches == 0 && missing == 0 ? "Info" : "Warning");
        }, ct);
    }

    private async Task InstallHelperAsync(DeviceRole role, CancellationToken ct)
    {
        if (!TryGetRoleDevice(role, out var device, out var label)) return;
        var apk = HelperApkPath.Trim();
        if (!File.Exists(apk))
        {
            Status = $"Helper APK not found: {apk}";
            AddRow("Helper", "Blocked", Status, "Warning");
            return;
        }

        await RunBusyAsync($"Installing helper on {label}...", async token =>
        {
            var svc = new HelperAppService(_host.Client, _log);
            var ok = await svc.InstallAsync(device, apk, token);
            Status = ok ? $"Helper installed on {label}." : $"Helper install failed on {label}.";
            AddRow("Helper", ok ? "Installed" : "Failed", Status, ok ? "Info" : "Warning");
        }, ct);
    }

    private async Task ProbeHelperAsync(DeviceRole role, CancellationToken ct)
    {
        if (!TryGetRoleDevice(role, out var device, out var label)) return;
        await RunBusyAsync($"Probing helper on {label}...", async token =>
        {
            var svc = new HelperAppService(_host.Client, _log);
            if (!await svc.IsInstalledAsync(device, token))
            {
                Status = $"Helper is not installed on {label}.";
                AddRow("Helper", "Missing", Status, "Warning");
                return;
            }

            var results = await svc.ProbeAllAsync(device, token);
            var ok = results.Count(kv => kv.Value);
            var failed = string.Join(", ", results.Where(kv => !kv.Value).Select(kv => kv.Key));
            Status = ok == results.Count
                ? $"Helper healthy on {label}: {ok}/{results.Count} authorities."
                : $"Helper partial on {label}: {ok}/{results.Count} healthy. Failed: {failed}.";
            AddRow("Helper", ok == results.Count ? "Healthy" : "Partial", Status, ok == results.Count ? "Info" : "Warning");
        }, ct);
    }

    private async Task UninstallHelperAsync(DeviceRole role, CancellationToken ct)
    {
        if (!TryGetRoleDevice(role, out var device, out var label)) return;
        await RunBusyAsync($"Uninstalling helper from {label}...", async token =>
        {
            var svc = new HelperAppService(_host.Client, _log);
            var ok = await svc.UninstallAsync(device, token);
            var residue = await svc.ResidueCheckAsync(device, token);
            Status = ok && residue.IsClean
                ? $"Helper removed cleanly from {label}."
                : $"Helper uninstall on {label} needs review. Installed={residue.HelperInstalled}, tmp leftovers={residue.TempFilesLeft.Count}.";
            AddRow("Helper", ok && residue.IsClean ? "Removed" : "Review", Status, ok && residue.IsClean ? "Info" : "Warning");
        }, ct);
    }

    private async Task CheckShizukuAsync(DeviceRole role, CancellationToken ct)
    {
        if (!TryGetRoleDevice(role, out var device, out var label)) return;
        await RunBusyAsync($"Checking Shizuku on {label}...", async token =>
        {
            var svc = new ShizukuService(_host.Client, _log);
            var state = await svc.ProbeAsync(device, token);
            var runbook = ShizukuService.Runbook(state);
            Status = $"{label}: Shizuku {state}. {runbook}";
            AddRow("Shizuku", state.ToString(), Status, state == ShizukuState.Running ? "Info" : "Warning");
        }, ct);
    }

    private void InspectBackupFile(string path)
    {
        var ab = new AndroidBackupReader(_log).Sniff(path);
        if (ab is not null)
        {
            Status = $".ab archive: version {ab.FormatVersion}, compressed={ab.Compressed}, encryption={ab.EncryptionTag}, keyBlock={ab.HasEncryptionKeyBlock}.";
            AddRow("Backup interop", "ADB backup", Status);
            return;
        }

        Status = $"File is not recognized as a legacy Android .ab backup: {path}";
        AddRow("Backup interop", "Unknown", Status, "Warning");
    }

    private async Task InspectBackupDirectoryAsync(string path, CancellationToken ct)
    {
        var found = false;
        var appManager = new AppManagerBackupReader(_log);
        var dirs = appManager.EnumerateBackupDirs(path).Take(20).ToArray();
        foreach (var dir in dirs)
        {
            found = true;
            try
            {
                var handle = await appManager.ReadAsync(dir, ct);
                AddRow("Backup interop", "AppManager", $"{handle.Meta.PackageName}: {handle.Meta.Apks.Count} APK entries, {handle.ChecksumsByFileName.Count} checksum rows, {handle.BackupTime:yyyy-MM-dd HH:mm}.");
            }
            catch (Exception ex)
            {
                AddRow("Backup interop", "AppManager invalid", $"{dir}: {ex.Message}", "Warning");
            }
        }

        var oab = new OpenAndroidBackupReader(_log).Sniff(path);
        if (oab is not null)
        {
            found = true;
            AddRow("Backup interop", "Open Android Backup", $"{Path.GetFileName(oab.ArchivePath)}: {oab.ArchiveSizeBytes / 1024.0 / 1024.0:F1} MiB, sidecar={oab.HasSidecar}.");
        }

        Status = found
            ? $"Backup inspection complete for {path}."
            : $"No AppManager or Open Android Backup markers found under {path}.";
        AddRow("Backup interop", found ? "Complete" : "No markers", Status, found ? "Info" : "Warning");
    }

    private async Task RunBusyAsync(string busyStatus, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = busyStatus;
        try
        {
            await action(ct);
        }
        catch (OperationCanceledException)
        {
            Status = "Operation canceled.";
            AddRow("Operation", "Canceled", Status, "Warning");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Operations panel action failed");
            Status = $"Operation failed: {ex.Message}";
            AddRow("Operation", "Failed", Status, "Warning");
        }
        finally
        {
            IsBusy = false;
            RefreshDeviceLines();
            RefreshTrustedRows();
            RefreshBurstLine();
        }
    }

    private bool TryGetRoleDevice(DeviceRole role, out DeviceData device, out string label)
    {
        device = default!;
        var phone = _devices.RoleHolder(role);
        label = role.ToString();
        if (phone is null || !phone.IsAuthorized)
        {
            Status = $"Assign an authorized {role} device first.";
            AddRow("Device", "Blocked", Status, "Warning");
            return false;
        }

        var adbDevice = _host.GetDevices().FirstOrDefault(d => d.Serial == phone.Serial);
        if (adbDevice is null)
        {
            Status = $"{role} device disconnected.";
            AddRow("Device", "Disconnected", Status, "Warning");
            return false;
        }

        device = adbDevice;
        label = phone.ShortLabel;
        return true;
    }

    private void RefreshDeviceLines()
    {
        SourceLine = FormatRoleLine(DeviceRole.Source);
        DestinationLine = FormatRoleLine(DeviceRole.Destination);
    }

    private string FormatRoleLine(DeviceRole role)
    {
        var phone = _devices.RoleHolder(role);
        if (phone is null) return $"{role}: not assigned.";
        var posture = _host.GetDevices().FirstOrDefault(d => d.Serial == phone.Serial) is { } data
            ? _posture.Probe(data).SummaryLine()
            : "device disconnected";
        return $"{role}: {phone.ShortLabel} - {posture}";
    }

    private void RefreshSmartSwitchLine()
    {
        var result = SmartSwitchDetection.Probe();
        SmartSwitchLine = result.IsAvailable
            ? $"Installed: {result.Install}. Legacy={result.LegacyInstallDir ?? "n/a"}; Store={result.StorePackageDir ?? "n/a"}; backup root={result.BackupRoot ?? "not found"}."
            : $"Not installed. Expected backup root: {result.BackupRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Samsung", "SmartSwitch")}.";
    }

    private void RefreshTrustedRows()
    {
        TrustedPairs.Clear();
        foreach (var pair in _trusted.All)
            TrustedPairs.Add(new TrustedPairRowViewModel(pair));
        TrustedLine = TrustedPairs.Count == 0
            ? "No trusted pairs recorded yet."
            : $"{TrustedPairs.Count} trusted pair(s) recorded. Raw serials are not stored.";
    }

    private void RefreshBurstLine()
    {
        BurstLine = _burst.IsBurstEnabled
            ? "ADB Burst Mode is enabled for newly started ADB processes."
            : "ADB Burst Mode is disabled.";
    }

    private void AddRow(string area, string state, string detail, string severity = "Info")
    {
        Rows.Insert(0, new OperationStatusRowViewModel(area, state, detail, severity));
        while (Rows.Count > 100)
            Rows.RemoveAt(Rows.Count - 1);
    }

    private static string ResolveDefaultHelperApk()
    {
        var here = Path.GetDirectoryName(AppContext.BaseDirectory) ?? Environment.CurrentDirectory;
        return Path.Combine(here, "assets", "helper", "PhoneForkHelper.apk");
    }
}
