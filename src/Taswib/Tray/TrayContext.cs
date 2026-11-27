using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using static Taswib.Strings;

namespace Taswib;

/// <summary>Tray icon, menu, hotkeys, windows and update checks: everything the user sees outside the fix itself.</summary>
sealed class TrayContext
{
    readonly WindowIcon normalIcon = Asset("taswib.ico"), grayIcon = Asset("Assets/taswib-gray.ico");
    readonly TrayIcon tray = new() { ToolTipText = "Taswib", Menu = new NativeMenu() };
    /// <summary>"Last fix: N chars" shown in the tooltip; setting it redraws the tooltip.</summary>
    string LastFix { get => lastFix; set { lastFix = value; SetTip(); } }
    string lastFix = "";
    readonly HotkeyWindow win;
    readonly Updater updater;

    /// <summary>Sets up tray, hotkeys and update checks, then opens Settings, Welcome or News as needed.</summary>
    /// <param name="wake">Signaled when a second copy starts; this copy then opens Settings.</param>
    public TrayContext(EventWaitHandle wake)
    {
        ThreadPool.RegisterWaitForSingleObject(wake, (_, _) => Dispatcher.UIThread.Post(ShowSettings), null, Timeout.Infinite, false);
        tray.Icon = normalIcon;
        tray.Clicked += (_, _) => ShowSettings(); // one left click opens the app
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { tray });
        win = new HotkeyWindow(async id => await RunFix(id == 1 ? Mode.Fix : Mode.Clean));
        Clip.Owner = win.Handle; // our clipboard writes need an owner window
        ApplyHotkey();
        Settings.Changed += ApplyHotkey; // any save (Settings window, news, welcome) re-applies
        updater = new Updater(msg => tray.ToolTipText = msg);
        _ = updater.Check(silent: true, restart: false); // never restart on its own; the update applies on next start
        var recheck = new DispatcherTimer { Interval = TimeSpan.FromHours(4) }; // PCs that never restart still get updates
        recheck.Tick += (_, _) => _ = updater.Check(silent: true, restart: false);
        recheck.Start();
        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "--settings") >= 0) // used by the CI screenshots; handy as a shortcut too
        {
            var pi = Array.IndexOf(args, "--page");
            if (pi >= 0 && pi + 1 < args.Length && int.TryParse(args[pi + 1], out var pg)) SettingsWindow.LastPage = Math.Clamp(pg, 0, 5);
            After(800, ShowSettings);
        }
        else if (Settings.FirstRun) After(800, ShowWelcome);
        else After(1500, ShowNews);
    }

    /// <summary>Loads an icon bundled as an Avalonia resource.</summary>
    static WindowIcon Asset(string path) => new(AssetLoader.Open(new Uri($"avares://Taswib/{path}")));

    /// <summary>Runs an action once after a delay, on the UI thread.</summary>
    internal static void After(int ms, Action a) => DispatcherTimer.RunOnce(a, TimeSpan.FromMilliseconds(ms));

    /// <summary>Shows What's New once per version. Only releases that bring a new feature have an entry; the rest stay silent.</summary>
    async void ShowNews()
    {
        var s = Settings.Current;
        var cur = Program.Version.ToString();
        if (WhatsNew.Pending(Program.Version, s.LastNewsVersion) is { } n)
            await Ui.ShowAsync(new NewsWindow(n.V, Arabic ? n.Ar : n.En));
        if (s.LastNewsVersion != cur) { s.LastNewsVersion = cur; s.Save(); }
    }

    /// <summary>First run: shows the Welcome window, then Settings if the user asked for it.</summary>
    async void ShowWelcome()
    {
        Settings.Current.LastNewsVersion = Program.Version.ToString(); // new installs skip old news
        Settings.Current.Save(); // creates settings.json, so the welcome never shows again
        var w = new WelcomeWindow(Settings.Current.Hotkey);
        await Ui.ShowAsync(w);
        if (w.OpenSettings) ShowSettings();
    }

    /// <summary>Runs a fix or clean on the selection and confirms the result with a sound or a notification.</summary>
    async Task RunFix(Mode mode)
    {
        var (r, len) = await Fixer.Run(mode, Settings.Current);
        switch (r)
        {
            case Result.Fixed:
                if (Settings.Current.SoundOnFix) Sound.Done();
                else Toast.Show(T("fixed", len), ms: 1500); // no sound: still confirm visibly
                LastFix = T("lastfix", len);
                break;
            case Result.NothingSelected: Toast.Show(T("n_nosel"), ms: 3500); break;
            case Result.NoChange: Toast.Show(T("n_nochange"), ms: 2500); break;
            case Result.Failed: Toast.Show(T("n_failed"), warn: true); break;
        }
    }

    /// <summary>Rebuilds the tray right-click menu in the current language.</summary>
    void BuildMenu()
    {
        Ui.MirrorMenus();
        var m = tray.Menu!;
        m.Items.Clear();
        m.Items.Add(Item(T("fixnow"), '', async () => await RunFix(Mode.Fix)));
        m.Items.Add(Item(T("cleannow"), '', async () => await RunFix(Mode.Clean)));
        m.Items.Add(Item(T("settings"), '', ShowSettings));
        m.Items.Add(Item(T("update"), '', async () => await updater.Check()));
        m.Items.Add(Item(T("exit"), '', Exit));
    }

    /// <summary>One tray menu entry with a glyph icon.</summary>
    static NativeMenuItem Item(string text, char glyph, Action click)
    {
        var i = new NativeMenuItem(text) { Icon = Ui.Glyph(glyph) };
        i.Click += (_, _) => click();
        return i;
    }

    /// <summary>Removes the tray icon and ends the app.</summary>
    void Exit()
    {
        tray.IsVisible = false;
        (Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    /// <summary>Sets the tray tooltip: the hotkey (or "disabled") plus the last fix.</summary>
    void SetTip()
    {
        var s = Settings.Current;
        var text = $"Taswib — {(s.Enabled ? s.Hotkey : T("disabled"))}" + (lastFix == "" ? "" : $" · {lastFix}");
        tray.ToolTipText = text.Length > 127 ? text[..127] : text; // Windows keeps 127 characters
    }

    /// <summary>Applies settings to the tray (menu, icon, tip) and re-registers both hotkeys; warns if one is taken.</summary>
    void ApplyHotkey()
    {
        var s = Settings.Current;
        BuildMenu();
        win.Unregister();
        tray.Icon = s.Enabled ? normalIcon : grayIcon;
        SetTip();
        if (!s.Enabled || settingsOpen) return; // hotkeys stay released while Settings is open
        foreach (var (id, hk) in new[] { (1, s.Hotkey), (2, s.CleanHotkey) })
        {
            if (string.IsNullOrWhiteSpace(hk)) continue;
            if (!Settings.TryParseHotkey(hk, out var mod, out var key) || !win.Register(id, mod, key))
            { Log.Write($"hotkey {hk} not registered"); Toast.Show(T("taken", hk), warn: true, ms: 4000); }
        }
    }

    /// <summary>Opens Settings (or focuses it if open), reopening after a language change. Hotkeys are off meanwhile.</summary>
    async void ShowSettings()
    {
        if (settingsOpen) { openForm?.Activate(); return; } // already open: bring it to the front
        settingsOpen = true;
        win.Unregister(); // else the global hotkey swallows the key you are trying to record
        SettingsWindow f;
        do
        {
            f = openForm = new SettingsWindow(Settings.Current, Startup.IsOn, report => updater.Check(report: report));
            await Ui.ShowAsync(f);
        } while (f.Reopen);
        settingsOpen = false; openForm = null;
        ApplyHotkey();
    }
    bool settingsOpen;
    SettingsWindow? openForm;
}
