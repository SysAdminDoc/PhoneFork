using CommunityToolkit.Mvvm.ComponentModel;
using PhoneFork.Core.Models;

namespace PhoneFork.App.ViewModels;

public partial class WifiSsidRowViewModel : ObservableObject
{
    public WifiNetwork Network { get; }

    [ObservableProperty] private string _typedPsk = "";

    public WifiSsidRowViewModel(WifiNetwork n) { Network = n; }

    public string Ssid => Network.Ssid;
    public string Auth => Network.Auth.ToString();
    public bool Hidden => Network.Hidden;
}
