using Microsoft.Win32;

namespace Taswib;

// "Start with Windows": a value under the current user's Run key.
static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsOn => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue("Taswib") != null;

    public static void Set(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (on) k?.SetValue("Taswib", $"\"{Environment.ProcessPath}\"");
            else k?.DeleteValue("Taswib", false);
        }
        catch (Exception e) { Log.Write("startup entry change failed", e); }
    }
}
