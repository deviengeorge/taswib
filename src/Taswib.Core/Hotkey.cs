namespace Taswib.Core;

// Hotkey text "Ctrl+Shift+Space" <-> (RegisterHotKey modifier flags, key name). No Windows types, so it is testable anywhere.
public static class Hotkey
{
    public const uint Alt = 1, Ctrl = 2, Shift = 4, Win = 8;

    // An unknown modifier fails instead of being dropped: "Crtl+K" must not register a bare K.
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

    public static string Format(uint mod, string key) =>
        string.Join('+', new[] { ((mod & Ctrl) != 0, "Ctrl"), ((mod & Shift) != 0, "Shift"), ((mod & Alt) != 0, "Alt"), ((mod & Win) != 0, "Win") }
            .Where(x => x.Item1).Select(x => x.Item2).Append(key));
}
