using System.Diagnostics;
using System.Net.Http.Json;
using System.Drawing.Imaging;
using Velopack;
using Velopack.Sources;
using static Taswib.Strings;

namespace Taswib;

// Tray icon, menu, hotkeys, windows and update checks: everything the user sees outside the fix itself.
sealed class TrayContext : ApplicationContext
{
    readonly Icon normalIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!;
    readonly NotifyIcon tray = new() { Visible = true, Text = "Taswib" };
    Icon? grayIcon;
    string lastFix = "";
    readonly HotkeyWindow win;

    public TrayContext(EventWaitHandle wake)
    {
        var ui = new Control(); _ = ui.Handle; // handle on the UI thread, so the wait callback can hop back with BeginInvoke
        ThreadPool.RegisterWaitForSingleObject(wake, (_, _) => { try { ui.BeginInvoke(ShowSettings); } catch { } }, null, Timeout.Infinite, false);
        tray.Icon = normalIcon;
        tray.ContextMenuStrip = new ContextMenuStrip();
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); }; // one left click opens the app
        win = new HotkeyWindow(async id => await RunFix(id == 1 ? Mode.Fix : Mode.Clean));
        ApplyHotkey();
        _ = CheckUpdate(silent: true);
        var recheck = new System.Windows.Forms.Timer { Interval = 4 * 60 * 60 * 1000 }; // PCs that never restart still get updates
        recheck.Tick += (_, _) => _ = CheckUpdate(silent: true, restart: false);
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

    static void After(int ms, Action a)
    {
        var t = new System.Windows.Forms.Timer { Interval = ms };
        t.Tick += (_, _) => { t.Stop(); t.Dispose(); a(); };
        t.Start();
    }

    // Only releases that bring a new feature have a WhatsNew entry; everything else stays silent.
    async void ShowNews()
    {
        var s = Settings.Current;
        var cur = Program.Version.ToString();
        if (WhatsNew.Pending(Program.Version, s.LastNewsVersion) is { } n)
            await Ui.ShowAsync(new NewsWindow(n.V, Arabic ? n.Ar : n.En));
        if (s.LastNewsVersion != cur) { s.LastNewsVersion = cur; s.Save(); }
    }

    async void ShowWelcome()
    {
        Settings.Current.LastNewsVersion = Program.Version.ToString(); // new installs skip old news
        Settings.Current.Save(); // creates settings.json, so the welcome never shows again
        var w = new WelcomeWindow(Settings.Current.Hotkey);
        await Ui.ShowAsync(w);
        if (w.OpenSettings) ShowSettings();
    }

    async Task RunFix(Mode mode)
    {
        var r = await Fixer.Run(mode);
        switch (r)
        {
            case Result.Fixed:
                if (Settings.Current.SoundOnFix) System.Media.SystemSounds.Asterisk.Play();
                else tray.ShowBalloonTip(1500, "Taswib", T("fixed", Fixer.LastLen), ToolTipIcon.Info); // no sound: still confirm visibly
                lastFix = T("lastfix", Fixer.LastLen); SetTip();
                break;
            case Result.NothingSelected: tray.ShowBalloonTip(3500, "Taswib", T("n_nosel"), ToolTipIcon.Info); break;
            case Result.NoChange: tray.ShowBalloonTip(2500, "Taswib", T("n_nochange"), ToolTipIcon.Info); break;
            case Result.Failed: tray.ShowBalloonTip(3000, "Taswib", T("n_failed"), ToolTipIcon.Warning); break;
        }
    }

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
            new ToolStripMenuItem(T("update"), Icons.Glyph('\uE895'), async (_, _) => await CheckUpdate()),
            new ToolStripMenuItem(T("exit"), Icons.Glyph('\uE7E8'), (_, _) => { tray.Visible = false; Application.Exit(); }),
        });
    }

    void SetTip()
    {
        var s = Settings.Current;
        var text = $"Taswib — {(s.Enabled ? s.Hotkey : T("disabled"))}" + (lastFix == "" ? "" : $" · {lastFix}");
        tray.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon.Text throws above 63 characters
    }

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

    async void ShowSettings()
    {
        if (settingsOpen) { openForm?.Activate(); return; } // already open: bring it to the front
        settingsOpen = true;
        win.Unregister(); // else the global hotkey swallows the key you are trying to record
        SettingsWindow f;
        do
        {
            f = openForm = new SettingsWindow(Settings.Current, Startup.IsOn, (s, startup) => { s.Save(); Startup.Set(startup); ApplyHotkey(); }, report => CheckUpdate(report: report));
            await Ui.ShowAsync(f);
        } while (f.Reopen);
        settingsOpen = false; openForm = null;
        ApplyHotkey();
    }
    bool settingsOpen;
    SettingsWindow? openForm;

    // Installed (Setup.exe): download + restart into the new version. Portable exe: just open the release page.
    // report != null (Settings button): show the result there; otherwise as a tray balloon.
    bool retryPending;
    async Task CheckUpdate(bool silent = false, bool restart = true, Action<string>? report = null)
    {
        void Say(string msg, ToolTipIcon icon = ToolTipIcon.Info, int ms = 3000)
        {
            if (report != null) report(msg); else tray.ShowBalloonTip(ms, "Taswib", msg, icon);
        }
        try
        {
            if (!silent) Say(T("chk"));
            var feed = Environment.GetEnvironmentVariable("TASWIB_UPDATE_FEED"); // CI update test serves releases from a local folder
            var mgr = string.IsNullOrEmpty(feed) ? new UpdateManager(new GithubSource($"https://github.com/{Links.Repo}", null, false)) : new UpdateManager(feed);
            if (mgr.IsInstalled)
            {
                var info = await mgr.CheckForUpdatesAsync();
                if (info == null) { if (!silent) Say(T("uptodate")); return; }
                if (!silent) Say(T("updating", info.TargetFullRelease.Version));
                var last = -1;
                await mgr.DownloadUpdatesAsync(info, pct =>
                {
                    if (silent || pct / 10 == last) return; // every 10%, not every tick
                    last = pct / 10;
                    var msg = T("dl", pct);
                    if (report != null) report(msg); else tray.Text = msg;
                });
                if (restart) { if (!silent) Say(T("restart")); mgr.ApplyUpdatesAndRestart(info); }
                else Say(T("ready", info.TargetFullRelease.Version), ToolTipIcon.Info, 5000); // Velopack applies a downloaded update on next start; WaitExitThenApplyUpdates would kill the app after 60 s
                return;
            }
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Taswib");
            var rel = await http.GetFromJsonAsync<Release>($"https://api.github.com/repos/{Links.Repo}/releases/latest");
            var latest = Version.Parse(rel!.tag_name.TrimStart('v'));
            if (latest > Program.Version)
            {
                if (silent) Say(T("newver", latest.ToString(3)), ToolTipIcon.Info, 5000); // startup check: never pop a browser
                else { Say(T("newver_open", latest.ToString(3))); Process.Start(new ProcessStartInfo(rel.html_url) { UseShellExecute = true }); }
            }
            else if (!silent) Say(T("uptodate"));
        }
        catch (Exception e)
        {
            Log.Write("update check failed", e);
            var offline = false;
            for (var x = e; x != null; x = x.InnerException) if (x is System.Net.Http.HttpRequestException or System.Net.Sockets.SocketException) offline = true;
            if (!silent) Say(T(offline ? "updfail" : "updfail2"), ToolTipIcon.Warning, 5000);
            else if (offline && !retryPending) // e.g. started at login before Wi-Fi connected: don't wait for the 4-hour check
            {
                retryPending = true;
                After(30 * 60 * 1000, () => { retryPending = false; _ = CheckUpdate(silent: true, restart: false); }); // no restart mid-use
            }
        }
    }

    record Release(string tag_name, string html_url);
}
