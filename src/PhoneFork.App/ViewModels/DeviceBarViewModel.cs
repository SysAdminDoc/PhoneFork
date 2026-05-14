using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.App.ViewModels;

public partial class DeviceCardViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    public PhoneInfo Phone { get; }

    [ObservableProperty] private DeviceRole _role;

    public DeviceCardViewModel(PhoneInfo phone, DeviceService devices)
    {
        Phone = phone;
        _devices = devices;
        _role = devices.RoleOf(phone.Serial);
    }

    partial void OnRoleChanged(DeviceRole value) => _devices.AssignRole(Phone.Serial, value);

    public string DisplayName => Phone.DisplayName;
    public string Serial => Phone.Serial;
    public string AndroidLine => string.IsNullOrEmpty(Phone.OneUiVersion)
        ? $"Android {Phone.AndroidVersion}"
        : $"Android {Phone.AndroidVersion} \u00B7 One UI {FormatOneUi(Phone.OneUiVersion)}";

    public string StatusText => Phone.IsAuthorized ? "Ready" : "Unauthorized — accept USB debugging prompt";
    public bool IsAuthorized => Phone.IsAuthorized;

    private static string FormatOneUi(string raw)
    {
        // ro.build.version.oneui is encoded as <major><minor><patch>, e.g. 80000 = 8.0.0
        if (raw.Length >= 5 && int.TryParse(raw, out var n))
            return $"{n / 10000}.{(n / 100) % 100}.{n % 100}";
        return raw;
    }
}

public partial class DeviceBarViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly ILogger _log;

    public ObservableCollection<DeviceCardViewModel> Cards { get; } = new();

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasSource;
    [ObservableProperty] private bool _hasDestination;
    [ObservableProperty] private bool _isReady;
    [ObservableProperty] private bool _hasDevices;
    [ObservableProperty] private bool _showWirelessPairing;
    [ObservableProperty] private bool _isPairingBusy;
    [ObservableProperty] private string _pairHostPort = "";
    [ObservableProperty] private string _pairCode = "";
    [ObservableProperty] private string _connectHostPort = "";
    [ObservableProperty] private string _pairingQrText = "";
    [ObservableProperty] private string _pairStatus = "Pair with the phone's Wireless debugging pairing endpoint, then connect to its wireless ADB endpoint.";

    public DeviceBarViewModel(DeviceService devices, AdbHostService host, ILogger log)
    {
        _devices = devices;
        _host = host;
        _log = log;
        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(Rebuild);
        Rebuild();
    }

    [RelayCommand]
    private void Refresh() => _devices.Refresh();

    [RelayCommand]
    private void ToggleWirelessPairing() => ShowWirelessPairing = !ShowWirelessPairing;

    [RelayCommand]
    private void ParsePairingQr()
    {
        var parsed = AdbPairingService.ParsePairingQr(PairingQrText.Trim());
        if (parsed is null)
        {
            PairStatus = "That QR text was not recognized. Use Android's Wireless debugging pairing code screen.";
            return;
        }

        PairCode = parsed.Value.Code;
        if (parsed.Value.ServiceName.Contains(':', StringComparison.Ordinal))
            PairHostPort = parsed.Value.ServiceName;

        PairStatus = string.IsNullOrWhiteSpace(PairHostPort)
            ? "Pairing code parsed. Enter the pairing IP:port shown on the phone."
            : "Pairing QR parsed. Review the endpoint, then pair.";
    }

    private bool CanPair() =>
        !IsPairingBusy
        && !string.IsNullOrWhiteSpace(PairHostPort)
        && !string.IsNullOrWhiteSpace(PairCode);

    private bool CanConnect() =>
        !IsPairingBusy
        && !string.IsNullOrWhiteSpace(ConnectHostPort);

    private bool CanDisconnect() => !IsPairingBusy;

    [RelayCommand(CanExecute = nameof(CanPair))]
    private async Task PairAsync(CancellationToken ct)
    {
        IsPairingBusy = true;
        PairStatus = $"Pairing {PairHostPort.Trim()}…";
        try
        {
            var svc = new AdbPairingService(_host.AdbPath, _log);
            var result = await svc.PairAsync(PairHostPort.Trim(), PairCode.Trim(), ct);
            PairStatus = result.Success
                ? "Paired. Enter the wireless ADB connect endpoint, then connect."
                : CleanAdbResult("Pair failed", result.Output, result.Error);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Wireless ADB pair failed");
            PairStatus = $"Pair failed: {ex.Message}";
        }
        finally
        {
            IsPairingBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken ct)
    {
        IsPairingBusy = true;
        PairStatus = $"Connecting {ConnectHostPort.Trim()}…";
        try
        {
            var svc = new AdbPairingService(_host.AdbPath, _log);
            var result = await svc.ConnectAsync(ConnectHostPort.Trim(), ct);
            PairStatus = result.Success
                ? CleanAdbResult("Connected", result.Output, result.Error)
                : CleanAdbResult("Connect failed", result.Output, result.Error);
            _devices.Refresh();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Wireless ADB connect failed");
            PairStatus = $"Connect failed: {ex.Message}";
        }
        finally
        {
            IsPairingBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync(CancellationToken ct)
    {
        IsPairingBusy = true;
        var target = string.IsNullOrWhiteSpace(ConnectHostPort) ? null : ConnectHostPort.Trim();
        PairStatus = target is null ? "Disconnecting wireless ADB devices…" : $"Disconnecting {target}…";
        try
        {
            var svc = new AdbPairingService(_host.AdbPath, _log);
            var result = await svc.DisconnectAsync(target, ct);
            PairStatus = CleanAdbResult(result.Success ? "Disconnected" : "Disconnect failed", result.Output, result.Error);
            _devices.Refresh();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Wireless ADB disconnect failed");
            PairStatus = $"Disconnect failed: {ex.Message}";
        }
        finally
        {
            IsPairingBusy = false;
        }
    }

    private void Rebuild()
    {
        Cards.Clear();
        foreach (var p in _devices.Phones)
            Cards.Add(new DeviceCardViewModel(p, _devices));

        HasDevices = Cards.Count > 0;
        HasSource = _devices.RoleHolder(DeviceRole.Source) is not null;
        HasDestination = _devices.RoleHolder(DeviceRole.Destination) is not null;
        IsReady = HasSource && HasDestination;
        Status = Cards.Count switch
        {
            0 => "Plug both phones in and accept the USB debugging prompt on each.",
            1 => "One device detected. Plug in the second phone and authorize ADB.",
            _ when IsReady => $"Ready — {_devices.RoleHolder(DeviceRole.Source)!.DisplayName} \u2192 {_devices.RoleHolder(DeviceRole.Destination)!.DisplayName}",
            _ => "Assign Source and Destination roles below to enable migration.",
        };
    }

    partial void OnIsPairingBusyChanged(bool value) => NotifyPairingCommands();
    partial void OnPairHostPortChanged(string value) => NotifyPairingCommands();
    partial void OnPairCodeChanged(string value) => NotifyPairingCommands();
    partial void OnConnectHostPortChanged(string value) => NotifyPairingCommands();

    private void NotifyPairingCommands()
    {
        PairCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    private static string CleanAdbResult(string prefix, string output, string error)
    {
        var parts = new[] { output.Trim(), error.Trim() }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var detail = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(detail) ? $"{prefix}." : $"{prefix}: {detail}";
    }
}
