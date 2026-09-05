using System.Windows;
using System.Windows.Interop;
using PhoneFork.App.ViewModels;
using PhoneFork.Core.Services;

namespace PhoneFork.App.Views;

public partial class MainWindow : Window
{
    private readonly WindowStateStore _windowState;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => TryApplyDarkTitleBar();
        DataContext = new MainViewModel(
            App.Current.Devices,
            App.Current.AdbHost,
            App.Current.Posture,
            App.Current.WirelessPolicy,
            App.Current.TrustedPairs,
            App.Current.Log);

        // F129 - a migration often spans several launches, so reopen where the user left off.
        _windowState = new WindowStateStore(App.Current.Log);
        RestoreWindowState();
        Closing += (_, _) => SaveWindowState();
    }

    private void RestoreWindowState()
    {
        var saved = _windowState.Load();
        if (saved is null) return;

        // Only honour a position that still lands on the current desktop. A window last closed on
        // a monitor that has since been unplugged would otherwise open where it cannot be reached.
        var virtualScreen = new[]
        {
            (SystemParameters.VirtualScreenLeft,
             SystemParameters.VirtualScreenTop,
             SystemParameters.VirtualScreenWidth,
             SystemParameters.VirtualScreenHeight),
        };

        if (WindowStateStore.IsOnAnyDisplay(saved, virtualScreen))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = saved.Left;
            Top = saved.Top;
            Width = Math.Max(saved.Width, MinWidth);
            Height = Math.Max(saved.Height, MinHeight);
        }

        if (saved.Maximized)
            WindowState = WindowState.Maximized;

        if (saved.SelectedTab >= 0 && saved.SelectedTab < DomainTabs.Items.Count)
            DomainTabs.SelectedIndex = saved.SelectedTab;
    }

    private void SaveWindowState()
    {
        // RestoreBounds carries the pre-maximise rectangle; Left/Top/Width/Height would report the
        // maximised frame and lose the size to restore to.
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        _windowState.Save(new WindowStateSnapshot
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = WindowState == WindowState.Maximized,
            SelectedTab = DomainTabs.SelectedIndex,
        });
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
