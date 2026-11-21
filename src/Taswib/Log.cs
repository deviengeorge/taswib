using System.Reflection;
using System.Runtime.InteropServices;
using WinClip = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Taswib;

/// <summary>Tiny file log. Never records the text being fixed, only lengths and outcomes.</summary>
static class Log
{
    static readonly object L = new();
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Taswib");
    static readonly string LogPath = Path.Combine(Dir, "taswib.log");

    public static void Write(string msg)
    {
        try
        {
            lock (L)
            {
                Directory.CreateDirectory(Dir);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 256 * 1024) File.Move(LogPath, LogPath + ".old", true);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
            }
        }
        catch { /* logging must never break the app */ }
    }

    public static void Write(string msg, Exception e) => Write($"{msg}: {e.GetType().Name}: {e.Message}");

    public static string Report()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        var layouts = "?";
        try
        {
            var n = GetKeyboardLayoutList(0, null);
            var l = new nint[n]; GetKeyboardLayoutList(n, l);
            layouts = string.Join(' ', l.Select(h => ((int)h & 0xFFFF).ToString("X4")));
        }
        catch { }
        var hist = "?";
        try { hist = WinClip.IsHistoryEnabled().ToString(); } catch { }
        string tail;
        try { tail = string.Join(Environment.NewLine, File.ReadLines(LogPath).TakeLast(80)); } catch { tail = "(no log yet)"; }
        return $"""
            Taswib {ver.Major}.{ver.Minor}.{ver.Build}
            OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})
            .NET: {RuntimeInformation.FrameworkDescription}
            UI culture: {System.Globalization.CultureInfo.CurrentUICulture.Name}
            Keyboard layouts (hex): {layouts}
            Clipboard history enabled: {hist}
            Process: {Environment.ProcessPath}
            Settings: {Settings.Current.ToJson()}
            --- last log lines ---
            {tail}
            """;
    }

    [DllImport("user32.dll")] static extern int GetKeyboardLayoutList(int n, nint[]? list);
}
