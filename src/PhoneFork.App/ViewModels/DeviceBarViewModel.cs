using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneFork.Core.Models;
using PhoneFork.Core.Services;

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

    public ObservableCollection<DeviceCardViewModel> Cards { get; } = new();

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasSource;
    [ObservableProperty] private bool _hasDestination;
    [ObservableProperty] private bool _isReady;

    public DeviceBarViewModel(DeviceService devices)
    {
        _devices = devices;
        _devices.PhonesChanged += (_, __) => Application.Current.Dispatcher.Invoke(Rebuild);
        Rebuild();
    }

    [RelayCommand]
    private void Refresh() => _devices.Refresh();

    private void Rebuild()
    {
        Cards.Clear();
        foreach (var p in _devices.Phones)
            Cards.Add(new DeviceCardViewModel(p, _devices));

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
}
