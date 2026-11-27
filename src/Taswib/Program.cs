using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Velopack;

namespace Taswib;

/// <summary>Entry point: single instance, crash logging, then starts Avalonia, which starts the tray (see <see cref="App"/>).</summary>
static class Program
{
    /// <summary>Assembly version trimmed to Major.Minor.Build (the 4th part is always 0).</summary>
    public static Version Version { get { var v = Assembly.GetExecutingAssembly().GetName().Version!; return new Version(v.Major, v.Minor, v.Build); } }

    /// <summary>Signaled by a second copy of the app; the tray then opens Settings.</summary>
    internal static EventWaitHandle Wake = null!;

    /// <summary>Runs Velopack hooks, enforces one instance (a second launch wakes the first), then runs Avalonia until Exit.</summary>
    [STAThread]
    static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Startup.Set(false)) // no dead "Start with Windows" entry after uninstall
            .Run(); // handles installer/update hooks, must be first
        using var mutex = new Mutex(true, "Taswib.SingleInstance", out var first);
        using var wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Taswib.Wake");
        if (!first) { wake.Set(); return; } // tell the running copy to open Settings
        Wake = wake;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("fatal", (Exception)e.ExceptionObject);
        Log.Write($"start {Version} | {RuntimeInformation.OSDescription}");
        // No windows at start, so only the tray menu's Exit ends the app.
        AppBuilder.Configure<App>().UseWin32().UseSkia().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}
