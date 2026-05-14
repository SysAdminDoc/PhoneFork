using System.Windows;
using System.Windows.Interop;
using PhoneFork.App.ViewModels;

namespace PhoneFork.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => TryApplyDarkTitleBar();
        DataContext = new MainViewModel(App.Current.Devices, App.Current.AdbHost, App.Current.Log);
    }

    private void TryApplyDarkTitleBar()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            return;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var enabled = 1;
        var result = DwmSetWindowAttribute(hwnd, DwmWindowAttributeUseImmersiveDarkMode, ref enabled, sizeof(int));
        if (result != 0)
            _ = DwmSetWindowAttribute(hwnd, DwmWindowAttributeUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
    }

    private const int DwmWindowAttributeUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmWindowAttributeUseImmersiveDarkMode = 20;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
}
