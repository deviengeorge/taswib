using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Velopack;

namespace Taswib;

/// <summary>Entry point: single instance, crash logging, then hands off to TrayContext.</summary>
static class Program
{
    /// <summary>Assembly version trimmed to Major.Minor.Build (the 4th part is always 0).</summary>
    public static Version Version { get { var v = Assembly.GetExecutingAssembly().GetName().Version!; return new Version(v.Major, v.Minor, v.Build); } }

    /// <summary>Runs Velopack hooks, enforces one instance (a second launch wakes the first), then starts the tray.</summary>
    [STAThread]
    static void Main()
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Startup.Set(false)) // no dead "Start with Windows" entry after uninstall
            .Run(); // handles installer/update hooks, must be first
        using var mutex = new Mutex(true, "Taswib.SingleInstance", out var first);
        using var wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Taswib.Wake");
        if (!first) { wake.Set(); return; } // tell the running copy to open Settings
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Log.Write("ui exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("fatal", (Exception)e.ExceptionObject);
        Log.Write($"start {Version} | {RuntimeInformation.OSDescription}");
        // Avalonia draws the windows; WinForms' message loop below also pumps Avalonia's messages.
        Ui.Setup();
        var ctx = new TrayContext(wake);
        Log.Write($"ready in {(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds:0} ms"); // process start to tray icon up
        Application.Run(ctx);
    }
}
