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
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<Card, string>(nameof(Title), "");
    public static readonly StyledProperty<string> DescProperty = AvaloniaProperty.Register<Card, string>(nameof(Desc), "");
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Desc { get => GetValue(DescProperty); set => SetValue(DescProperty, value); }
}

/// <summary>Click, then press a shortcut. Values use WinForms <see cref="Keys"/> names, as RegisterHotKey needs them.</summary>
public sealed class HotkeyBox : Border
{
    readonly TextBlock text = new();
    readonly DispatcherTimer hintTimer = new() { Interval = TimeSpan.FromMilliseconds(2200) };
    bool captured;
    public bool AllowClear { get; set; }
    public string Value { get; private set; } = "";
    public event Action<string>? Changed;

    public HotkeyBox()
    {
        Focusable = true; Child = text;
        hintTimer.Tick += (_, _) => { hintTimer.Stop(); Show(); };
    }

    public void Init(string v) { Value = v; Show(); }
    public void Set(string v) { Value = v; captured = true; Show(); Changed?.Invoke(v); }

    void Show(string? hint = null)
    {
        text.Classes.Set("hint", hint != null);
        text.Text = hint ?? (IsFocused && !captured ? "…" : Value == "" ? "—" : Value); // "…" only until a key is pressed
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e) { Focus(); e.Handled = true; }
    protected override void OnGotFocus(GotFocusEventArgs e) { captured = false; Show(); base.OnGotFocus(e); }
    protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { Show(); base.OnLostFocus(e); }

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
            Show(T("hk_bare")); hintTimer.Stop(); hintTimer.Start();
            return;
        }
        var mods = (m.HasFlag(KeyModifiers.Control) ? Keys.Control : 0) | (m.HasFlag(KeyModifiers.Shift) ? Keys.Shift : 0) | (m.HasFlag(KeyModifiers.Alt) ? Keys.Alt : 0);
        Set(Settings.FormatHotkey(mods, key));
    }

    // Keys that are safe to own globally with no modifier.
    static bool BareOk(Keys k) => (k >= Keys.F1 && k <= Keys.F24)
        || k is Keys.Insert or Keys.Pause or Keys.Scroll or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Delete;
}
