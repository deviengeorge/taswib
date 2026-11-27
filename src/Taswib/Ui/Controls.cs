using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using static Taswib.Strings;
// WinForms is referenced too (tray, clipboard); these aliases pick the Avalonia type for the names both define.
using Window = Avalonia.Controls.Window;
using Button = Avalonia.Controls.Button;
using ListBox = Avalonia.Controls.ListBox;
using Border = Avalonia.Controls.Border;
using TextBlock = Avalonia.Controls.TextBlock;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using FlowDirection = Avalonia.Media.FlowDirection;
using Keys = System.Windows.Forms.Keys;
using Application = Avalonia.Application;
using FontFamily = Avalonia.Media.FontFamily;

namespace Taswib;

/// <summary>Settings row: title and description, with a control (Content) on the far side. Look is in App.axaml.</summary>
public sealed class Card : ContentControl
{
    /// <summary>Styled property behind <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<Card, string>(nameof(Title), "");
    /// <summary>Styled property behind <see cref="Desc"/>.</summary>
    public static readonly StyledProperty<string> DescProperty = AvaloniaProperty.Register<Card, string>(nameof(Desc), "");
    /// <summary>Row heading.</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>Smaller description under the heading.</summary>
    public string Desc { get => GetValue(DescProperty); set => SetValue(DescProperty, value); }
}

/// <summary>Click, then press a shortcut. Values use WinForms <see cref="Keys"/> names, as RegisterHotKey needs them.</summary>
public sealed class HotkeyBox : Border
{
    readonly TextBlock text = new();
    readonly DispatcherTimer hintTimer = new() { Interval = TimeSpan.FromMilliseconds(2200) };

    // State the text depends on. Changing any of them (or IsFocused) redraws through OnPropertyChanged.
    /// <summary>Styled property behind <see cref="Value"/>.</summary>
    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<HotkeyBox, string>(nameof(Value), "");
    static readonly StyledProperty<bool> CapturedProperty = AvaloniaProperty.Register<HotkeyBox, bool>(nameof(Captured));
    static readonly StyledProperty<string?> HintProperty = AvaloniaProperty.Register<HotkeyBox, string?>(nameof(Hint));
    /// <summary>True once a key was pressed in this focus; until then the box shows "…".</summary>
    bool Captured { get => GetValue(CapturedProperty); set => SetValue(CapturedProperty, value); }
    /// <summary>Temporary warning shown instead of the value; cleared by the hint timer.</summary>
    string? Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }
    /// <summary>When true, Delete clears the shortcut (for optional hotkeys).</summary>
    public bool AllowClear { get; set; }
    /// <summary>Current shortcut, e.g. <c>Ctrl+Shift+F</c>; empty when cleared.</summary>
    public string Value { get => GetValue(ValueProperty); private set => SetValue(ValueProperty, value); }
    /// <summary>Raised with the new value when the shortcut is recorded, reset or cleared.</summary>
    public event Action<string>? Changed;

    /// <summary>Creates a focusable box; the hint timer brings the value back after a warning.</summary>
    public HotkeyBox()
    {
        Focusable = true; Child = text;
        hintTimer.Tick += (_, _) => { hintTimer.Stop(); Hint = null; };
        Render();
    }

    /// <summary>Sets the starting value without raising <see cref="Changed"/>.</summary>
    public void Init(string v) => Value = v;
    /// <summary>Sets the value and raises <see cref="Changed"/>.</summary>
    public void Set(string v) { Value = v; Captured = true; Changed?.Invoke(v); }

    /// <summary>Redraws whenever the shown state changes, so no caller has to remember to.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ValueProperty || e.Property == CapturedProperty || e.Property == HintProperty || e.Property == IsFocusedProperty) Render();
    }

    /// <summary>Shows the hint if any, else the value ("…" while waiting for a key, "—" when empty).</summary>
    void Render()
    {
        text.Classes.Set("hint", Hint != null);
        text.Text = Hint ?? (IsFocused && !Captured ? "…" : Value == "" ? "—" : Value); // "…" only until a key is pressed
    }

    /// <summary>Click focuses the box so it starts listening.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e) { Focus(); e.Handled = true; }
    /// <summary>Starts a new capture and shows the waiting mark.</summary>
    protected override void OnGotFocus(GotFocusEventArgs e) { Captured = false; base.OnGotFocus(e); }

    /// <summary>Records the pressed shortcut. Waits past lone modifiers; rejects bare keys unsafe to own system-wide.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab) { base.OnKeyDown(e); return; } // Tab still moves focus away
        e.Handled = true;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.Escape) return; // wait for the real key
        var m = e.KeyModifiers;
        if (e.Key == Key.Delete && m == KeyModifiers.None && AllowClear) { Set(""); return; }
        // ponytail: Avalonia and WinForms key names match for the keys people bind (letters, digits, F-keys, Insert, Pause...); an unmatched one beeps.
        if (!Enum.TryParse<Keys>(e.Key.ToString(), out var key)) { System.Media.SystemSounds.Beep.Play(); return; }
        if (m == KeyModifiers.None && !BareOk(key))
        {
            System.Media.SystemSounds.Beep.Play(); // a bare letter would be hijacked system-wide; keep old value
            Hint = T("hk_bare"); hintTimer.Stop(); hintTimer.Start();
            return;
        }
        var mods = (m.HasFlag(KeyModifiers.Control) ? Taswib.Core.Hotkey.Ctrl : 0) | (m.HasFlag(KeyModifiers.Shift) ? Taswib.Core.Hotkey.Shift : 0) | (m.HasFlag(KeyModifiers.Alt) ? Taswib.Core.Hotkey.Alt : 0);
        Set(Taswib.Core.Hotkey.Format(mods, key.ToString()));
    }

    /// <summary>Keys that are safe to own globally with no modifier.</summary>
    static bool BareOk(Keys k) => (k >= Keys.F1 && k <= Keys.F24)
        || k is Keys.Insert or Keys.Pause or Keys.Scroll or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Delete;
}
