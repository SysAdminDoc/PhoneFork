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
        : $"Android {Phone.AndroidVersion} \u00B7 One UI {Phone.FormattedOneUiVersion}";

    public string StatusText => Phone.IsAuthorized ? "Ready" : "Unauthorized — accept USB debugging prompt";
    public bool IsAuthorized => Phone.IsAuthorized;
}

public partial class DeviceBarViewModel : ObservableObject
{
    private readonly DeviceService _devices;
    private readonly AdbHostService _host;
    private readonly WirelessPolicy _wireless;
    private readonly TrustedPairRegistry _trusted;
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
    [ObservableProperty] private bool _wirelessOptedIn;
    [ObservableProperty] private bool _allowUnpatchedOverride;
    [ObservableProperty] private string _wirelessSessionStatus = "Wireless ADB is off. USB is the default trust posture.";
    [ObservableProperty] private string _mdnsStatus = "Tap Discover to scan the LAN for wireless ADB services.";
    [ObservableProperty] private bool _isMdnsBusy;

    public ObservableCollection<MdnsServiceRowViewModel> MdnsServices { get; } = new();

    public DeviceBarViewModel(DeviceService devices, AdbHostService host, SecurityPostureService _posture, WirelessPolicy wireless, TrustedPairRegistry trusted, ILogger log)
    {
        _devices = devices;
        _host = host;
        _wireless = wireless;
        _trusted = trusted;
        _log = log;
        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(() =>
        {
            Rebuild();
            UpdateTrustForCurrentDevices();
        });
        Rebuild();
        UpdateTrustForCurrentDevices();
        RefreshWirelessSessionStatus();
    }

    /// <summary>
    /// For each currently visible authorized device, refresh its trusted-pair record.
    /// The registry only stores hashes; the visible label is rebuilt from PhoneInfo
    /// each session and remains volatile.
    /// </summary>
    private void UpdateTrustForCurrentDevices()
    {
        foreach (var card in Cards)
        {
            if (!card.IsAuthorized) continue;
            var transport = SecurityPostureService.ClassifyTransport(card.Serial);
            _trusted.Touch(card.Serial, card.DisplayName, transport,
                lastEndpoint: transport == AdbTransport.Tcp ? card.Serial : null);
        }
    }

    [RelayCommand]
    private void ToggleWirelessSession()
    {
        if (_wireless.WirelessOptedIn)
        {
            _wireless.KillWireless();
        }
        else
        {
            _wireless.OptInWireless();
        }
        WirelessOptedIn = _wireless.WirelessOptedIn;
        RefreshWirelessSessionStatus();
    }

    private void RefreshWirelessSessionStatus()
    {
        WirelessOptedIn = _wireless.WirelessOptedIn;
        AllowUnpatchedOverride = _wireless.AllowUnpatchedOverride;
        WirelessSessionStatus = _wireless.WirelessOptedIn
            ? $"Wireless ADB session active. Expires {_wireless.SessionExpiresAt.ToLocalTime():HH:mm}. Patch level >= 2026-05-01 required."
            : "Wireless ADB is off. USB is the default trust posture.";
    }

    partial void OnAllowUnpatchedOverrideChanged(bool value)
    {
        _wireless.AllowUnpatchedOverride = value;
        RefreshWirelessSessionStatus();
    }

    [RelayCommand]
    private void Refresh() => _devices.Refresh();

    [RelayCommand]
    private async Task DiscoverMdnsAsync(CancellationToken ct)
    {
        IsMdnsBusy = true;
        MdnsStatus = "Scanning the LAN for wireless ADB services…";
        try
        {
            var svc = new AdbPairingService(_host.AdbPath, _log);
            var services = await svc.ListMdnsServicesAsync(ct);
            MdnsServices.Clear();
            foreach (var s in services)
            {
                var hash = SerialHash.Of(s.HostPort);
                var trusted = _trusted.Get(s.HostPort);
                var label = trusted?.Label ?? (string.IsNullOrEmpty(s.Instance) ? s.HostPort : s.Instance);
                MdnsServices.Add(new MdnsServiceRowViewModel(s, label, trusted is not null, hash));
            }
            MdnsStatus = MdnsServices.Count == 0
                ? "No wireless ADB services discovered. Ensure both devices are on the same Wi-Fi network and have Wireless debugging enabled."
                : $"{MdnsServices.Count} service(s) found. Click Reconnect on a trusted entry, or paste its endpoint into Connect above.";
        }
        catch (Exception ex)
        {
            _log.Error(ex, "mDNS discovery failed");
            MdnsStatus = $"mDNS discovery failed: {ex.Message}";
        }
        finally
        {
            IsMdnsBusy = false;
        }
    }

    [RelayCommand]
    private void ReconnectMdns(MdnsServiceRowViewModel? row)
    {
        if (row is null) return;
        ConnectHostPort = row.Service.HostPort;
        PairStatus = $"Endpoint {row.Service.HostPort} loaded into Connect. Verify trust, then Connect.";
    }

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
        var gate = _wireless.EvaluateHostPort(PairHostPort.Trim());
        if (!gate.IsAllowed)
        {
            PairStatus = $"Pair refused. {gate.Reason}";
            _log.Warning("Wireless ADB pair blocked: {Reason}", gate.Reason);
            return;
        }

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
            RefreshWirelessSessionStatus();
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync(CancellationToken ct)
    {
        var gate = _wireless.EvaluateHostPort(ConnectHostPort.Trim());
        if (!gate.IsAllowed)
        {
            PairStatus = $"Connect refused. {gate.Reason}";
            _log.Warning("Wireless ADB connect blocked: {Reason}", gate.Reason);
            return;
        }

        IsPairingBusy = true;
        PairStatus = $"Connecting {ConnectHostPort.Trim()}…";
        try
        {
            var svc = new AdbPairingService(_host.AdbPath, _log);
            var endpoint = ConnectHostPort.Trim();
            var result = await svc.ConnectAsync(endpoint, ct);
            if (result.Success)
            {
                _trusted.Touch(endpoint, $"Wireless {endpoint}", AdbTransport.Tcp, endpoint);
            }
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
            RefreshWirelessSessionStatus();
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

/// <summary>
/// One discovered mDNS service row in the DeviceBar's reconnect surface (F005).
/// </summary>
public sealed class MdnsServiceRowViewModel
{
    public MdnsService Service { get; }
    public string Label { get; }
    public bool IsTrusted { get; }
    public string SerialHashShort { get; }

    public MdnsServiceRowViewModel(MdnsService service, string label, bool isTrusted, string serialHash)
    {
        Service = service;
        Label = label;
        IsTrusted = isTrusted;
        SerialHashShort = serialHash;
    }

    public string Endpoint => Service.HostPort;
    public string ServiceType => Service.ServiceType;
    public string TrustLine => IsTrusted ? $"Trusted ({SerialHashShort})" : "Untrusted endpoint";
}
