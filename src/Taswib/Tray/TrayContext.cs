using System.Drawing.Imaging;
using static Taswib.Strings;

namespace Taswib;

/// <summary>Tray icon, menu, hotkeys, windows and update checks: everything the user sees outside the fix itself.</summary>
sealed class TrayContext : ApplicationContext
{
    readonly Icon normalIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!;
    readonly NotifyIcon tray = new() { Visible = true, Text = "Taswib" };
    Icon? grayIcon;
    /// <summary>"Last fix: N chars" shown in the tooltip; setting it redraws the tooltip.</summary>
    string LastFix { get => lastFix; set { lastFix = value; SetTip(); } }
    string lastFix = "";
    readonly HotkeyWindow win;
    readonly Updater updater;

    /// <summary>Sets up tray, hotkeys and update checks, then opens Settings, Welcome or News as needed.</summary>
    /// <param name="wake">Signaled when a second copy starts; this copy then opens Settings.</param>
    public TrayContext(EventWaitHandle wake)
    {
        var ui = new Control(); _ = ui.Handle; // handle on the UI thread, so the wait callback can hop back with BeginInvoke
        ThreadPool.RegisterWaitForSingleObject(wake, (_, _) => { try { ui.BeginInvoke(ShowSettings); } catch { } }, null, Timeout.Infinite, false);
        tray.Icon = normalIcon;
        tray.ContextMenuStrip = new ContextMenuStrip();
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); }; // one left click opens the app
        win = new HotkeyWindow(async id => await RunFix(id == 1 ? Mode.Fix : Mode.Clean));
        Clip.Owner = win.Handle; // our clipboard writes need an owner window
        ApplyHotkey();
        Settings.Changed += ApplyHotkey; // any save (Settings window, news, welcome) re-applies
        updater = new Updater(tray);
        _ = updater.Check(silent: true, restart: false); // never restart on its own; the update applies on next start
        var recheck = new System.Windows.Forms.Timer { Interval = 4 * 60 * 60 * 1000 }; // PCs that never restart still get updates
        recheck.Tick += (_, _) => _ = updater.Check(silent: true, restart: false);
        recheck.Start();
        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "--settings") >= 0) // used by the CI screenshots; handy as a shortcut too
        {
            var pi = Array.IndexOf(args, "--page");
            if (pi >= 0 && pi + 1 < args.Length && int.TryParse(args[pi + 1], out var pg)) SettingsWindow.LastPage = Math.Clamp(pg, 0, 5);
            After(800, ShowSettings);
        }
        else if (Settings.FirstRun) After(800, ShowWelcome); // once the message loop is running
        else After(1500, ShowNews);
    }

    /// <summary>Runs an action once after a delay, on the UI thread (WinForms timer).</summary>
    internal static void After(int ms, Action a)
    {
        var t = new System.Windows.Forms.Timer { Interval = ms };
        t.Tick += (_, _) => { t.Stop(); t.Dispose(); a(); };
        t.Start();
    }

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

    /// <summary>Runs a fix or clean on the selection and confirms the result with a sound or tray balloon.</summary>
    async Task RunFix(Mode mode)
    {
        var (r, len) = await Fixer.Run(mode, Settings.Current);
        switch (r)
        {
            case Result.Fixed:
                if (Settings.Current.SoundOnFix) System.Media.SystemSounds.Asterisk.Play();
                else tray.ShowBalloonTip(1500, "Taswib", T("fixed", len), ToolTipIcon.Info); // no sound: still confirm visibly
                LastFix = T("lastfix", len);
                break;
            case Result.NothingSelected: tray.ShowBalloonTip(3500, "Taswib", T("n_nosel"), ToolTipIcon.Info); break;
            case Result.NoChange: tray.ShowBalloonTip(2500, "Taswib", T("n_nochange"), ToolTipIcon.Info); break;
            case Result.Failed: tray.ShowBalloonTip(3000, "Taswib", T("n_failed"), ToolTipIcon.Warning); break;
        }
    }

    /// <summary>Rebuilds the tray right-click menu in the current language.</summary>
    void BuildMenu()
    {
        var m = tray.ContextMenuStrip!;
        m.Items.Clear();
        m.RightToLeft = Arabic ? RightToLeft.Yes : RightToLeft.No;
        m.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem(T("fixnow"), Icons.Glyph('\uE8AC'), async (_, _) => await RunFix(Mode.Fix)),
            new ToolStripMenuItem(T("cleannow"), Icons.Glyph('\uE74D'), async (_, _) => await RunFix(Mode.Clean)),
            new ToolStripMenuItem(T("settings"), Icons.Glyph('\uE713'), (_, _) => ShowSettings()),
            new ToolStripMenuItem(T("update"), Icons.Glyph('\uE895'), async (_, _) => await updater.Check()),
            new ToolStripMenuItem(T("exit"), Icons.Glyph('\uE7E8'), (_, _) => { tray.Visible = false; Application.Exit(); }),
        });
    }

    /// <summary>Sets the tray tooltip: the hotkey (or "disabled") plus the last fix.</summary>
    void SetTip()
    {
        var s = Settings.Current;
        var text = $"Taswib — {(s.Enabled ? s.Hotkey : T("disabled"))}" + (lastFix == "" ? "" : $" · {lastFix}");
        tray.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon.Text throws above 63 characters
    }

    /// <summary>Grayscale copy of the icon, shown in the tray while Taswib is disabled.</summary>
    static Icon Gray(Icon icon)
    {
        using var src = icon.ToBitmap();
        var dst = new Bitmap(src.Width, src.Height);
        using (var g = Graphics.FromImage(dst))
        using (var ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix(new[] { new[] { .3f, .3f, .3f, 0, 0 }, new[] { .59f, .59f, .59f, 0, 0 }, new[] { .11f, .11f, .11f, 0, 0 }, new[] { 0f, 0, 0, 1, 0 }, new[] { 0f, 0, 0, 0, 1 } }));
            g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
        }
        return Icon.FromHandle(dst.GetHicon());
    }

    /// <summary>Applies settings to the tray (menu, icon, tip) and re-registers both hotkeys; warns if one is taken.</summary>
    void ApplyHotkey()
    {
        var s = Settings.Current;
        BuildMenu();
        win.Unregister();
        tray.Icon = s.Enabled ? normalIcon : (grayIcon ??= Gray(normalIcon));
        SetTip();
        if (!s.Enabled || settingsOpen) return; // hotkeys stay released while Settings is open
        foreach (var (id, hk) in new[] { (1, s.Hotkey), (2, s.CleanHotkey) })
        {
            if (string.IsNullOrWhiteSpace(hk)) continue;
            if (!Settings.TryParseHotkey(hk, out var mod, out var key) || !win.Register(id, mod, key))
            { Log.Write($"hotkey {hk} not registered"); tray.ShowBalloonTip(4000, "Taswib", T("taken", hk), ToolTipIcon.Warning); }
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
