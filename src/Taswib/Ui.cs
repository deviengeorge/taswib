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

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}

/// <summary>XAML text lookup: <c>Text="{l:Tr enabled}"</c>.</summary>
public sealed class Tr : Avalonia.Markup.Xaml.MarkupExtension
{
    public Tr(string key) => Key = key;
    public string Key { get; set; }
    public override object ProvideValue(IServiceProvider _) => T(Key);
}

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

static class Ui
{
    // Bundled (OFL), so Arabic looks the same on every PC; English keeps Segoe UI.
    public static readonly FontFamily ArabicFont = new($"avares://{typeof(Ui).Assembly.GetName().Name}/Assets/Fonts#Cairo");

    public static void Setup() => AppBuilder.Configure<App>().UseWin32().UseSkia().SetupWithoutStarting();

    /// <summary>Show a window and wait until it closes (the tray app has no owner window for ShowDialog).</summary>
    public static Task ShowAsync(Window w)
    {
        var done = new TaskCompletionSource();
        w.Closed += (_, _) => done.TrySetResult();
        if (Arabic) { w.FlowDirection = FlowDirection.RightToLeft; w.FontFamily = ArabicFont; w.Classes.Add("ar"); } // mirrors the whole layout
        w.Show(); w.Activate();
        return done.Task;
    }

    public static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}

public partial class SettingsWindow : Window
{
    internal static int LastPage;
    readonly Settings s;
    readonly Action<Settings, bool> apply;
    bool startupOn;
    readonly DispatcherTimer savedTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };

    /// <summary>Set when the language changed: the caller reopens the window so it is laid out for the new language.</summary>
    public bool Reopen { get; private set; }

    public SettingsWindow() : this(new Settings(), false, (_, _) => { }, _ => Task.CompletedTask) { } // XAML previewer

    public SettingsWindow(Settings s, bool startupOn, Action<Settings, bool> apply, Func<Action<string>, Task> checkUpdate)
    {
        this.s = s; this.startupOn = startupOn; this.apply = apply;
        InitializeComponent();
        savedTimer.Tick += (_, _) => { savedTimer.Stop(); Saved.Text = ""; };

        Nav.SelectionChanged += (_, _) => ShowPage(Nav.SelectedIndex);
        Nav.SelectedIndex = LastPage;

        Tog(Enabled, s.Enabled, v => s.Enabled = v);
        Tog(Startup, startupOn, v => this.startupOn = v);
        Seg(Lang, s.Language == "ar" ? 1 : 0, i => { s.Language = i == 1 ? "ar" : "en"; Reopen = true; Dispatcher.UIThread.Post(Close); });

        FixKey.Init(s.Hotkey); FixKey.Changed += k => { s.Hotkey = k; Save(); };
        CleanKey.Init(s.CleanHotkey); CleanKey.Changed += k => { s.CleanHotkey = k; Save(); };
        FixReset.Click += (_, _) => FixKey.Set(new Settings().Hotkey);
        CleanReset.Click += (_, _) => CleanKey.Set(new Settings().CleanHotkey);

        Seg(ScopeSeg, (int)s.Scope, i => s.Scope = (Scope)i);
        Seg(DirSeg, (int)s.Direction, i => s.Direction = (Taswib.Core.Direction)i);
        Tog(Switch, s.SwitchLayout, v => s.SwitchLayout = v);
        Tog(Restore, s.RestoreClipboard, v => s.RestoreClipboard = v);
        Tog(Sound, s.SoundOnFix, v => s.SoundOnFix = v);
        Tog(Punct, s.CleanPunctuation, v => s.CleanPunctuation = v);
        Tog(Norm, s.CleanNormalize, v => s.CleanNormalize = v);

        ContactName.Text = Links.Name;
        ContactRow("c_email", Links.Email, "btn_send", "mailto:" + Links.Email);
        ContactRow("c_whatsapp", Links.PhoneDisplay, "btn_chat", Links.WhatsApp);
        ContactRow("c_linkedin", Links.LinkedInShort, "open", Links.LinkedIn);
        if (Links.TelegramUser != "") ContactRow("c_telegram", "@" + Links.TelegramUser, "btn_chat", "https://t.me/" + Links.TelegramUser);

        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        VersionCard.Title = $"Taswib {ver.Major}.{ver.Minor}.{ver.Build}";
        Update.Click += async (_, _) =>
        {
            var old = Update.Content;
            Update.IsEnabled = false;
            await checkUpdate(msg => Dispatcher.UIThread.Post(() => Update.Content = msg));
            await Task.Delay(4000);
            Update.Content = old; Update.IsEnabled = true;
        };
        Github.Click += (_, _) => Ui.Open("https://github.com/deviengeorge/taswib");
        CopyReport.Click += async (_, _) =>
        {
            System.Windows.Forms.Clipboard.SetText(Log.Report());
            var old = CopyReport.Content; CopyReport.Content = T("copied");
            await Task.Delay(1500);
            CopyReport.Content = old;
        };
        OpenLogs.Click += (_, _) => { Directory.CreateDirectory(Log.Dir); Ui.Open(Log.Dir); };
    }

    void ShowPage(int page)
    {
        if (page < 0) return;
        LastPage = page;
        for (var i = 0; i < Pages.Children.Count; i++) Pages.Children[i].IsVisible = i == page;
        PageTitle.Text = ((TextBlock)((StackPanel)((ListBoxItem)Nav.Items[page]!).Content!).Children[1]).Text;
    }

    void Save()
    {
        apply(s, startupOn);
        Saved.Text = T("saved");
        savedTimer.Stop(); savedTimer.Start();
    }

    // Initial value first, then listen: setting it must not count as a change.
    void Tog(ToggleSwitch t, bool v, Action<bool> set)
    {
        t.IsChecked = v;
        t.IsCheckedChanged += (_, _) => { set(t.IsChecked == true); Save(); };
    }

    void Seg(ListBox l, int selected, Action<int> set)
    {
        l.SelectedIndex = selected;
        l.SelectionChanged += (_, _) => { if (l.SelectedIndex >= 0) { set(l.SelectedIndex); Save(); } };
    }

    void ContactRow(string title, string value, string btn, string url)
    {
        var b = new Button { Content = T(btn) };
        b.Click += (_, _) => Ui.Open(url);
        ContactPage.Children.Add(new Card { Title = T(title), Desc = value, Content = b });
    }
}

public partial class WelcomeWindow : Window
{
    public bool OpenSettings { get; private set; }
    public WelcomeWindow() : this("Insert") { }
    public WelcomeWindow(string hotkey)
    {
        InitializeComponent();
        Body.Text = T("w_body", hotkey);
        OpenSettingsBtn.Click += (_, _) => { OpenSettings = true; Close(); };
        Ok.Click += (_, _) => Close();
        Opened += (_, _) => { Sample.Focus(); Sample.SelectAll(); }; // selected, so the shortcut works immediately
    }
}

public partial class NewsWindow : Window
{
    public NewsWindow() : this(new Version(0, 0), "") { }
    public NewsWindow(Version v, string body)
    {
        InitializeComponent();
        Title = Head.Text = T("news_title", $"{v.Major}.{v.Minor}");
        Body.Text = body;
        Ok.Click += (_, _) => Close();
    }
}

/// <summary>Tray menu icons (WinForms menu): 16px bitmaps from the Windows icon font.</summary>
static class Icons
{
    public static System.Drawing.Bitmap Glyph(char c)
    {
        var bmp = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using var f = new System.Drawing.Font("Segoe MDL2 Assets", 10f);
        System.Windows.Forms.TextRenderer.DrawText(g, c.ToString(), f, new System.Drawing.Rectangle(0, 0, 16, 16), System.Drawing.Color.FromArgb(70, 70, 70),
            System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPadding);
        return bmp;
    }
}
