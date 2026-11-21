using System.Text.Json;
using System.Text.Json.Serialization;
using Taswib.Core;

namespace Taswib;

public enum Scope { SelectionOnly, SelectionOrLine, SelectionOrAll }

public sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public string Hotkey { get; set; } = "Pause";
    public Scope Scope { get; set; } = Scope.SelectionOrLine;
    public Direction Direction { get; set; } = Direction.Auto;
    public bool RestoreClipboard { get; set; } = true;

    public static Settings Current { get; private set; } = Load();

    static readonly string PathFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Taswib", "settings.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathFile), Opt) ?? new(); }
        catch { return new(); } // missing or corrupt file: defaults
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFile)!);
        File.WriteAllText(PathFile, JsonSerializer.Serialize(this, Opt));
    }

    // Hotkey string "Ctrl+Shift+Space" <-> (modifier flags, key)
    public static bool TryParseHotkey(string s, out uint mod, out Keys key)
    {
        mod = 0; key = Keys.None;
        var parts = s.Split('+', StringSplitOptions.TrimEntries);
        foreach (var p in parts[..^1])
            mod |= p switch { "Alt" => 1u, "Ctrl" => 2u, "Shift" => 4u, "Win" => 8u, _ => 0u };
        return Enum.TryParse(parts[^1], out key) && key != Keys.None;
    }

    public static string FormatHotkey(Keys modifiers, Keys key)
    {
        var l = new List<string>();
        if (modifiers.HasFlag(Keys.Control)) l.Add("Ctrl");
        if (modifiers.HasFlag(Keys.Shift)) l.Add("Shift");
        if (modifiers.HasFlag(Keys.Alt)) l.Add("Alt");
        l.Add(key.ToString());
        return string.Join('+', l);
    }
}
