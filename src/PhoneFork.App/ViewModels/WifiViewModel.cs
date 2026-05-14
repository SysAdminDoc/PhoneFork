using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using QRCoder;
using Serilog;

namespace PhoneFork.App.ViewModels;

public partial class WifiViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly ILogger _log;

    public ObservableCollection<WifiSsidRowViewModel> Ssids { get; } = new();
    public ObservableCollection<WifiAuth> AuthChoices { get; } = new(Enum.GetValues<WifiAuth>());

    [ObservableProperty] private string _status = "Pick a source device and click Scan to list its SSIDs, or build a join-QR manually below.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasSsids;

    // CSC diff panel
    [ObservableProperty] private string _cscSource = "";
    [ObservableProperty] private string _cscDest = "";
    [ObservableProperty] private string _cscWarning = "";

    // Manual QR composer
    [ObservableProperty] private string _qrSsid = "";
    [ObservableProperty] private string _qrPsk = "";
    [ObservableProperty] private WifiAuth _qrAuth = WifiAuth.Wpa;
    [ObservableProperty] private bool _qrHidden;
    [ObservableProperty] private BitmapImage? _qrImage;
    [ObservableProperty] private string _qrPayload = "";
    [ObservableProperty] private string _savedQrPath = "";

    public WifiViewModel(DeviceService devices, AdbHostService host, ILogger log)
    {
        _devices = devices;
        _host = host;
        _log = log;
        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(() =>
        {
            ScanSourceCommand.NotifyCanExecuteChanged();
            ScanCscCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanScanSource() => !IsBusy && _devices.RoleHolder(DeviceRole.Source) is not null;
    private bool CanScanCsc() => !IsBusy && _devices.RoleHolder(DeviceRole.Source) is not null && _devices.RoleHolder(DeviceRole.Destination) is not null;

    [RelayCommand(CanExecute = nameof(CanScanSource))]
    private async Task ScanSourceAsync(CancellationToken ct)
    {
        var src = _devices.RoleHolder(DeviceRole.Source);
        if (src is null) return;
        var data = _host.GetDevices().FirstOrDefault(d => d.Serial == src.Serial);
        if (data is null) { Status = "Source device disconnected."; return; }
        IsBusy = true;
        Status = $"Scanning Wi-Fi on {src.DisplayName}…";
        try
        {
            var svc = new WifiSnapshotService(_host.Client, _log);
            var nets = await svc.ListSsidsAsync(data, ct);
            Ssids.Clear();
            foreach (var n in nets.OrderBy(n => n.Ssid, StringComparer.OrdinalIgnoreCase))
                Ssids.Add(new WifiSsidRowViewModel(n));
            HasSsids = Ssids.Count > 0;
            Status = $"{Ssids.Count} SSID(s) found on {src.DisplayName}. PSKs aren't recoverable without v0.7 helper APK — enter each PSK manually below to render a join-QR.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Wi-Fi scan failed");
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanScanCsc))]
    private async Task ScanCscAsync(CancellationToken ct)
    {
        var src = _devices.RoleHolder(DeviceRole.Source);
        var dst = _devices.RoleHolder(DeviceRole.Destination);
        if (src is null || dst is null) return;
        var srcData = _host.GetDevices().FirstOrDefault(d => d.Serial == src.Serial);
        var dstData = _host.GetDevices().FirstOrDefault(d => d.Serial == dst.Serial);
        if (srcData is null || dstData is null) { Status = "Device disconnected."; return; }

        IsBusy = true;
        var svc = new CscDiffService(_host.Client, _log);
        try
        {
            var srcSnap = await svc.CaptureAsync(srcData, ct);
            var dstSnap = await svc.CaptureAsync(dstData, ct);
            CscSource = Format(srcSnap);
            CscDest = Format(dstSnap);
            var diff = svc.Diff(srcSnap, dstSnap);
            CscWarning = diff.AnyMismatch
                ? "Mismatch detected — region-locked items (Samsung Pay, regional Health features) may not restore cleanly."
                : "No CSC / locale / country mismatch detected.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "CSC scan failed");
            Status = $"Region scan failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BuildQr()
    {
        if (string.IsNullOrWhiteSpace(QrSsid))
        {
            QrPayload = "";
            QrImage = null;
            return;
        }
        var net = new WifiNetwork { Ssid = QrSsid, Psk = QrPsk, Auth = QrAuth, Hidden = QrHidden };
        QrPayload = WifiQrService.BuildPayload(net);

        // Render to in-memory BitmapImage for the WPF Image control.
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(QrPayload, QRCodeGenerator.ECCLevel.Q);
        using var pngQr = new PngByteQRCode(qrData);
        var bytes = pngQr.GetGraphic(8);
        var bi = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        bi.BeginInit();
        bi.CacheOption = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        QrImage = bi;
    }

    [RelayCommand]
    private void SaveQr()
    {
        if (string.IsNullOrWhiteSpace(QrSsid)) return;
        var net = new WifiNetwork { Ssid = QrSsid, Psk = QrPsk, Auth = QrAuth, Hidden = QrHidden };
        var safe = LocalPathNames.SafeFileName(QrSsid, "wifi-network");
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhoneFork", "wifi-qrs");
        Directory.CreateDirectory(dir);
        var outPath = Path.Combine(dir, $"{safe}.png");
        WifiQrService.RenderPng(net, outPath);
        SavedQrPath = outPath;
    }

    [RelayCommand]
    private void UseRow(WifiSsidRowViewModel? row)
    {
        if (row is null) return;
        QrSsid = row.Network.Ssid;
        QrAuth = row.Network.Auth;
        QrHidden = row.Network.Hidden;
        QrPsk = row.TypedPsk;
        BuildQr();
    }

    private static string Format(CscSnapshot s)
        => $"CSC: {s.SalesCode}   Country: {s.CountryCode}   Locale: {s.Locale}   TZ: {s.Timezone}   Carrier: {s.CarrierIso}";

    partial void OnIsBusyChanged(bool value)
    {
        ScanSourceCommand.NotifyCanExecuteChanged();
        ScanCscCommand.NotifyCanExecuteChanged();
    }
}
