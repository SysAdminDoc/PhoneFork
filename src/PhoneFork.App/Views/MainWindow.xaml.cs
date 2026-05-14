using System.Windows;
using PhoneFork.App.ViewModels;

namespace PhoneFork.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(App.Current.Devices, App.Current.AdbHost, App.Current.Log);
    }
}
