using System.IO;
using System.Windows;
using PhoneFork.Core.Logging;
using PhoneFork.Core.Services;
using Serilog;

namespace PhoneFork.App;

/// <summary>
/// Composition root. Owns the singleton <see cref="AdbHostService"/> and
/// <see cref="DeviceService"/> instances accessed by all view-models.
/// </summary>
public partial class App : Application
{
    public ILogger Log { get; private set; } = null!;
    public AdbHostService AdbHost { get; private set; } = null!;
    public DeviceService Devices { get; private set; } = null!;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log = AuditLogger.Create();
        Log.Information("PhoneFork starting (pid {Pid})", Environment.ProcessId);

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Log.Fatal(ex.ExceptionObject as Exception, "AppDomain unhandled exception");
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Fatal(ex.Exception, "Dispatcher unhandled exception");
            MessageBox.Show(ex.Exception.ToString(), "PhoneFork crashed", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            Log.Error(ex.Exception, "Unobserved task exception");
            ex.SetObserved();
        };

        var adbPath = ResolveAdb();
        AdbHost = new AdbHostService(adbPath, Log);
        if (!AdbHost.EnsureServerRunning())
        {
            MessageBox.Show(
                $"Could not start ADB server.\n\nLooked for adb.exe at:\n  {adbPath}\n\n"
                + "Make sure the 'tools' folder ships next to PhoneFork.exe.",
                "PhoneFork", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        Log.Information("Step: creating DeviceService");
        Devices = new DeviceService(AdbHost, Log);
        Log.Information("Step: refreshing devices");
        Devices.Refresh();

        try
        {
            Log.Information("Step: constructing MainWindow");
            var window = new Views.MainWindow();
            Log.Information("Step: setting MainWindow + Show()");
            MainWindow = window;
            window.Show();
            Log.Information("MainWindow shown");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "MainWindow construction failed");
            MessageBox.Show(ex.ToString(), "PhoneFork — startup failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(3);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { AdbHost?.Dispose(); } catch { }
        Log?.Information("PhoneFork exit (code {Code})", e.ApplicationExitCode);
        base.OnExit(e);
    }

    private static string ResolveAdb()
    {
        var here = Path.GetDirectoryName(AppContext.BaseDirectory) ?? Environment.CurrentDirectory;
        var candidates = new[]
        {
            Path.Combine(here, "tools", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "adb.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PhoneFork", "tools", "adb.exe"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        return "adb.exe"; // PATH fallback
    }
}
