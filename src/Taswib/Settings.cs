using System.Text.Json;
using System.Text.Json.Serialization;
using Taswib.Core;

namespace Taswib;

public enum Scope { SelectionOnly, SelectionOrLine, SelectionOrAll }

public sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public string Hotkey { get; set; } = "Insert";
    public Scope Scope { get; set; } = Scope.SelectionOrAll;
    public Direction Direction { get; set; } = Direction.Auto;
    public bool RestoreClipboard { get; set; } = true;
    public bool SwitchLayout { get; set; } = true;
    public bool SoundOnFix { get; set; } = true;
    public string LastNewsVersion { get; set; } = "0.0.0";
    public string CleanHotkey { get; set; } = "Shift+Pause";
    public bool CleanPunctuation { get; set; } = true;
    public bool CleanNormalize { get; set; } = true;
    public string Language { get; set; } = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "ar" : "en";

    static readonly string PathFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Taswib", "settings.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    // Order matters: Current runs Load(), which needs PathFile and Opt already set.
    public static bool FirstRun { get; } = !File.Exists(PathFile);
    public static Settings Current { get; } = Load();

    static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathFile), Opt) ?? new(); }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        catch (Exception e) { Log.Write("settings unreadable, using defaults", e); return new(); }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Opt);

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFile)!);
        var tmp = PathFile + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opt)); File.Move(tmp, PathFile, overwrite: true);
    }

    // Text parsing lives in Taswib.Core.Hotkey (tested there); this only maps the key name to Keys.
    public static bool TryParseHotkey(string s, out uint mod, out Keys key)
    {
        key = Keys.None;
        return Taswib.Core.Hotkey.TryParse(s, out mod, out var name) && Enum.TryParse(name, out key) && key != Keys.None;
    }
}
