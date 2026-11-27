using System.Text.Json;
using System.Text.Json.Serialization;
using Taswib.Core;

namespace Taswib;

/// <summary>What to fix when nothing is selected: nothing, the current line, or the whole text box.</summary>
public enum Scope { SelectionOnly, SelectionOrLine, SelectionOrAll }

/// <summary>User settings, stored as JSON in %APPDATA%\Taswib\settings.json.</summary>
public sealed class Settings
{
    /// <summary>Master switch: when off, the hotkeys do nothing.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Fix shortcut as text, e.g. "Ctrl+Shift+Space".</summary>
    public string Hotkey { get; set; } = "Insert";
    /// <summary>What to fix when nothing is selected.</summary>
    public Scope Scope { get; set; } = Scope.SelectionOrAll;
    /// <summary>Conversion direction; Auto detects it per fix.</summary>
    public Direction Direction { get; set; } = Direction.Auto;
    /// <summary>Put the user's previous clipboard back after a fix.</summary>
    public bool RestoreClipboard { get; set; } = true;
    /// <summary>Switch the keyboard layout to the target language after a fix.</summary>
    public bool SwitchLayout { get; set; } = true;
    /// <summary>Play a short sound when a fix succeeds.</summary>
    public bool SoundOnFix { get; set; } = true;
    /// <summary>Last version whose What's New popup was shown, so each popup appears once.</summary>
    public string LastNewsVersion { get; set; } = "0.0.0";
    /// <summary>Clean shortcut as text; empty means no shortcut.</summary>
    public string CleanHotkey { get; set; } = "Shift+Pause";
    /// <summary>Clean turns , ? ; into Arabic punctuation after Arabic text.</summary>
    public bool CleanPunctuation { get; set; } = true;
    /// <summary>Clean unifies alef/yaa forms and strips tashkeel and tatweel.</summary>
    public bool CleanNormalize { get; set; } = true;
    /// <summary>UI language, "en" or "ar"; defaults to Arabic when Windows' UI language is Arabic.</summary>
    public string Language { get; set; } = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "ar" : "en";

    static readonly string PathFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Taswib", "settings.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    // Order matters: Current runs Load(), which needs PathFile and Opt already set.
    /// <summary>True when no settings file existed at startup; used to show the welcome window.</summary>
    public static bool FirstRun { get; } = !File.Exists(PathFile);
    /// <summary>The settings loaded at startup, shared app-wide.</summary>
    public static Settings Current { get; } = Load();

    /// <summary>Reads the settings file; missing or unreadable falls back to defaults (unreadable is logged).</summary>
    static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathFile), Opt) ?? new(); }
        catch (FileNotFoundException) { return new(); }
        catch (DirectoryNotFoundException) { return new(); }
        catch (Exception e) { Log.Write("settings unreadable, using defaults", e); return new(); }
    }

    /// <summary>Serializes the settings as indented JSON (used in the debug report).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Opt);

    /// <summary>Writes settings.json, then raises <see cref="Changed"/>.</summary>
    public void Save()
    {
        SaveFile();
        Changed?.Invoke(); // listeners (the tray) re-apply from here, so no caller has to remember to
    }

    /// <summary>Raised after every <see cref="Save"/>; the tray subscribes to re-register hotkeys and redraw.</summary>
    public static event Action? Changed;

    /// <summary>Writes to a temp file then moves it over, so a crash mid-write can't corrupt settings.</summary>
    void SaveFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathFile)!);
        var tmp = PathFile + ".tmp"; File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opt)); File.Move(tmp, PathFile, overwrite: true);
    }

    /// <summary>Parses hotkey text into modifier flags and a virtual-key code. Parsing lives in Taswib.Core.Hotkey.</summary>
    /// <returns>False if the text is invalid or the key name is unknown.</returns>
    public static bool TryParseHotkey(string s, out uint mod, out ushort vk)
    {
        vk = 0;
        return Taswib.Core.Hotkey.TryParse(s, out mod, out var name) && Taswib.Core.Hotkey.TryKey(name, out vk, out _);
    }
}
