using System.Reflection;
using System.Runtime.InteropServices;
using WinClip = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Taswib;

/// <summary>Tiny file log. Never records the text being fixed, only lengths and outcomes.</summary>
static class Log
{
    static readonly object L = new();
    /// <summary>Log folder: %LOCALAPPDATA%\Taswib.</summary>
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Taswib");
    static readonly string LogPath = Path.Combine(Dir, "taswib.log");

    /// <summary>Appends a timestamped line; rotates to .old past 256 KB. Never throws.</summary>
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

    /// <summary>Logs the message with the exception type and message (no stack trace).</summary>
    public static void Write(string msg, Exception e) => Write($"{msg}: {e.GetType().Name}: {e.Message}");

    /// <summary>Debug info for bug reports: version, OS, layouts, clipboard history, settings and last 80 log lines.</summary>
    public static string Report()
    {
        var layouts = "?";
        try
        {
            layouts = string.Join(' ', Win.Layouts().Select(h => ((int)h & 0xFFFF).ToString("X4")));
        }
        catch { }
        var hist = "?";
        try { hist = WinClip.IsHistoryEnabled().ToString(); } catch { }
        string tail;
        try { tail = string.Join(Environment.NewLine, File.ReadLines(LogPath).TakeLast(80)); } catch { tail = "(no log yet)"; }
        return $"""
            Taswib {Program.Version}
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
}
