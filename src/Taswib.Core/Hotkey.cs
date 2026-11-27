namespace Taswib.Core;

/// <summary>
/// Hotkey text "Ctrl+Shift+Space" &lt;-&gt; (RegisterHotKey modifier flags, key name).
/// No Windows types, so it is testable anywhere.
/// </summary>
public static class Hotkey
{
    /// <summary>RegisterHotKey modifier flags (MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN).</summary>
    public const uint Alt = 1, Ctrl = 2, Shift = 4, Win = 8;

    /// <summary>
    /// Splits "Mod+Mod+Key" into modifier flags and the key name. An unknown modifier fails instead of
    /// being dropped: "Crtl+K" must not register a bare K.
    /// </summary>
    /// <returns>False on an unknown modifier or an empty key name.</returns>
    public static bool TryParse(string s, out uint mod, out string key)
    {
        mod = 0; key = "";
        var parts = s.Split('+', StringSplitOptions.TrimEntries);
        foreach (var p in parts[..^1])
        {
            var m = p switch { "Alt" => Alt, "Ctrl" => Ctrl, "Shift" => Shift, "Win" => Win, _ => 0u };
            if (m == 0) return false;
            mod |= m;
        }
        key = parts[^1];
        return key != "";
    }

    /// <summary>Builds hotkey text in fixed order Ctrl+Shift+Alt+Win+Key; the inverse of <see cref="TryParse"/>.</summary>
    public static string Format(uint mod, string key) =>
        string.Join('+', new[] { ((mod & Ctrl) != 0, "Ctrl"), ((mod & Shift) != 0, "Shift"), ((mod & Alt) != 0, "Alt"), ((mod & Win) != 0, "Win") }
            .Where(x => x.Item1).Select(x => x.Item2).Append(key));
}
