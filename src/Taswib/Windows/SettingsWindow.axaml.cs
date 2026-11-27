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

        VersionCard.Title = $"Taswib {Program.Version}";
        Update.Click += async (_, _) =>
        {
            var old = Update.Content;
            Update.IsEnabled = false;
            await checkUpdate(msg => Dispatcher.UIThread.Post(() => Update.Content = msg));
            await Task.Delay(4000);
            Update.Content = old; Update.IsEnabled = true;
        };
        Github.Click += (_, _) => Ui.Open($"https://github.com/{Links.Repo}");
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
        PageTitle.Text = T((string)((ListBoxItem)Nav.Items[page]!).Tag!); // Tag = the nav item's text key
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
