using CommunityToolkit.Mvvm.ComponentModel;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public DeviceBarViewModel DeviceBar { get; }
    public AppsViewModel Apps { get; }
    public MediaViewModel Media { get; }
    public SettingsViewModel Settings { get; }
    public DebloatViewModel Debloat { get; }
    public WifiViewModel Wifi { get; }

    public MainViewModel(DeviceService devices, AdbHostService host, ILogger log)
    {
        DeviceBar = new DeviceBarViewModel(devices);
        Apps = new AppsViewModel(devices, host, log);
        Media = new MediaViewModel(devices, host, log);
        Settings = new SettingsViewModel(devices, host, log);
        Debloat = new DebloatViewModel(devices, host, log);
        Wifi = new WifiViewModel(devices, host, log);
    }
}
