using Microsoft.Win32;

namespace Taswib;

/// <summary>"Start with Windows": a value under the current user's Run key.</summary>
static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>True if the Run entry exists.</summary>
    public static bool IsOn => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue("Taswib") != null;

    /// <summary>Adds or removes the Run entry. Failures are logged, never thrown.</summary>
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
