using Avalonia.Controls;
using Avalonia.Threading;
using static Taswib.Strings;

namespace Taswib;

/// <summary>Settings window: sidebar with 6 pages; every change is saved and applied at once.</summary>
public partial class SettingsWindow : Window
{
    /// <summary>Page to show on open; remembered between openings, set by <c>--page</c>.</summary>
    internal static int LastPage;
    readonly Settings s;
    readonly DispatcherTimer savedTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };

    /// <summary>Set when the language changed: the caller reopens the window so it is laid out for the new language.</summary>
    public bool Reopen { get; private set; }

    /// <summary>Design-time constructor.</summary>
    public SettingsWindow() : this(new Settings(), false, _ => Task.CompletedTask) { } // XAML previewer

    /// <summary>Builds the window and wires every control to <paramref name="s"/>, which it edits in place.</summary>
    /// <param name="checkUpdate">Runs an update check that sends its messages to the given callback.</param>
    public SettingsWindow(Settings s, bool startupOn, Func<Action<string>, Task> checkUpdate)
    {
        this.s = s;
        InitializeComponent();
        savedTimer.Tick += (_, _) => { savedTimer.Stop(); Saved.Text = ""; };

        Nav.SelectionChanged += (_, _) => ShowPage(Nav.SelectedIndex);
        Nav.SelectedIndex = LastPage;

        Tog(Enabled, s.Enabled, v => s.Enabled = v);
        Tog(Startup, startupOn, Taswib.Startup.Set);
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
        Update.Click += (_, _) => Flash(Update, 4000, () => checkUpdate(msg => Dispatcher.UIThread.Post(() => Update.Content = msg)));
        Github.Click += (_, _) => Ui.Open($"https://github.com/{Links.Repo}");
        CopyReport.Click += (_, _) => Flash(CopyReport, 1500, async () =>
        {
            await Clipboard!.SetTextAsync(Log.Report());
            CopyReport.Content = T("copied");
        });
        OpenLogs.Click += (_, _) => { Directory.CreateDirectory(Log.Dir); Ui.Open(Log.Dir); };
    }

    /// <summary>Shows one page, hides the others, and sets the page title.</summary>
    void ShowPage(int page)
    {
        if (page < 0) return;
        LastPage = page;
        for (var i = 0; i < Pages.Children.Count; i++) Pages.Children[i].IsVisible = i == page;
        PageTitle.Text = T((string)((ListBoxItem)Nav.Items[page]!).Tag!); // Tag = the nav item's text key
    }

    /// <summary>
    /// Runs <paramref name="work"/> (which may change the button's label), keeps the result up for
    /// <paramref name="ms"/>, then restores the label. The button is disabled meanwhile, so it cannot double-run.
    /// </summary>
    static async void Flash(Button b, int ms, Func<Task> work)
    {
        var old = b.Content;
        b.IsEnabled = false;
        try { await work(); await Task.Delay(ms); }
        finally { b.Content = old; b.IsEnabled = true; }
    }

    /// <summary>Saves (which re-applies via <see cref="Settings.Changed"/>) and flashes "Saved" for a moment.</summary>
    void Save()
    {
        s.Save();
        Saved.Text = T("saved");
        savedTimer.Stop(); savedTimer.Start();
    }

    /// <summary>Binds a toggle to a setting. Initial value first, then listen: setting it must not count as a change.</summary>
    void Tog(ToggleSwitch t, bool v, Action<bool> set)
    {
        t.IsChecked = v;
        t.IsCheckedChanged += (_, _) => { set(t.IsChecked == true); Save(); };
    }

    /// <summary>Binds a segmented choice (ListBox) to a setting by index, same way as Tog.</summary>
    void Seg(ListBox l, int selected, Action<int> set)
    {
        l.SelectedIndex = selected;
        l.SelectionChanged += (_, _) => { if (l.SelectedIndex >= 0) { set(l.SelectedIndex); Save(); } };
    }

    /// <summary>Adds a Contact page card whose button opens the given link.</summary>
    void ContactRow(string title, string value, string btn, string url)
    {
        var b = new Button { Content = T(btn) };
        b.Click += (_, _) => Ui.Open(url);
        ContactPage.Children.Add(new Card { Title = T(title), Desc = value, Content = b });
    }
}
