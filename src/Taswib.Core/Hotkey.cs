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

    /// <summary>
    /// Key name to Windows virtual-key code. Names are the ones settings.json already holds (the old WinForms
    /// <c>Keys</c> names), and match Avalonia's <c>Key</c> names apart from case ("Oemcomma" vs "OemComma").
    /// </summary>
    static readonly Dictionary<string, ushort> Vk = BuildVk();
    /// <summary>Key names for <see cref="Vk"/> codes, first name wins ("PageUp", not "Prior").</summary>
    static readonly Dictionary<ushort, string> Names = Vk.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.First().Key);

    static Dictionary<string, ushort> BuildVk()
    {
        var d = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        for (var c = 'A'; c <= 'Z'; c++) d[c.ToString()] = c;
        for (var i = 0; i <= 9; i++) { d[$"D{i}"] = (ushort)('0' + i); d[$"NumPad{i}"] = (ushort)(0x60 + i); }
        for (var i = 1; i <= 24; i++) d[$"F{i}"] = (ushort)(0x6F + i);
        foreach (var (name, vk) in new (string, ushort)[]
        {
            ("Back", 0x08), ("Tab", 0x09), ("Enter", 0x0D), ("Return", 0x0D), ("Pause", 0x13), ("Escape", 0x1B), ("Space", 0x20),
            ("PageUp", 0x21), ("Prior", 0x21), ("PageDown", 0x22), ("Next", 0x22), ("End", 0x23), ("Home", 0x24),
            ("Left", 0x25), ("Up", 0x26), ("Right", 0x27), ("Down", 0x28), ("PrintScreen", 0x2C), ("Snapshot", 0x2C),
            ("Insert", 0x2D), ("Delete", 0x2E), ("Apps", 0x5D), ("Multiply", 0x6A), ("Add", 0x6B), ("Subtract", 0x6D),
            ("Decimal", 0x6E), ("Divide", 0x6F), ("Scroll", 0x91), ("OemSemicolon", 0xBA), ("Oemplus", 0xBB),
            ("Oemcomma", 0xBC), ("OemMinus", 0xBD), ("OemPeriod", 0xBE), ("OemQuestion", 0xBF), ("Oemtilde", 0xC0),
            ("OemOpenBrackets", 0xDB), ("OemPipe", 0xDC), ("OemCloseBrackets", 0xDD), ("OemQuotes", 0xDE), ("OemBackslash", 0xE2),
        }) d.TryAdd(name, vk);
        return d;
    }

    /// <summary>Looks up a key name (any case) and returns its virtual-key code and saved spelling.</summary>
    /// <returns>False for a name with no entry (the caller beeps or reports it).</returns>
    public static bool TryKey(string name, out ushort vk, out string canonical)
    {
        canonical = "";
        if (!Vk.TryGetValue(name, out vk)) return false;
        canonical = Names[vk];
        return true;
    }

    /// <summary>Keys safe to own system-wide with no modifier: F-keys and navigation keys, never letters.</summary>
    public static bool BareOk(ushort vk) => vk is >= 0x70 and <= 0x87 or 0x2D or 0x13 or 0x91 or 0x24 or 0x23 or 0x21 or 0x22 or 0x2E;
}
