using System.Diagnostics;
using System.Net.Http.Json;
using Velopack;
using Velopack.Sources;
using static Taswib.Strings;

namespace Taswib;

// Checks GitHub for a newer release.
// Installed copy (Setup.exe): download it and restart into it. Portable exe: point the user at the release page.
sealed class Updater(NotifyIcon tray)
{
    bool retryPending;

    // silent:  background check, only speaks when there is news.
    // restart: restart into a downloaded update now (false = it applies on next start).
    // report:  Settings page callback; without it messages go to the tray balloon.
    public async Task Check(bool silent = false, bool restart = true, Action<string>? report = null)
    {
        var say = new Say(tray, report, silent);
        try
        {
            say.Info(T("chk"));
            var mgr = Manager();
            if (mgr.IsInstalled) await UpdateInstalled(mgr, say, restart);
            else await CheckPortable(say);
        }
        catch (Exception e)
        {
            Log.Write("update check failed", e);
            var offline = IsOffline(e);
            say.Info(T(offline ? "updfail" : "updfail2"), ToolTipIcon.Warning, 5000);
            if (silent && offline) RetryLater();
        }
    }

    static UpdateManager Manager()
    {
        var feed = Environment.GetEnvironmentVariable("TASWIB_UPDATE_FEED"); // CI update test serves releases from a local folder
        return string.IsNullOrEmpty(feed) ? new UpdateManager(new GithubSource($"https://github.com/{Links.Repo}", null, false)) : new UpdateManager(feed);
    }

    static async Task UpdateInstalled(UpdateManager mgr, Say say, bool restart)
    {
        var info = await mgr.CheckForUpdatesAsync();
        if (info == null) { say.Info(T("uptodate")); return; }
        var version = info.TargetFullRelease.Version;
        say.Info(T("updating", version));
        var lastTenth = -1;
        await mgr.DownloadUpdatesAsync(info, pct =>
        {
            if (pct / 10 == lastTenth) return; // every 10%, not every tick
            lastTenth = pct / 10;
            say.Progress(T("dl", pct));
        });
        if (restart) { say.Info(T("restart")); mgr.ApplyUpdatesAndRestart(info); }
        else say.Always(T("ready", version), 5000); // Velopack applies it on next start; WaitExitThenApplyUpdates would kill the app after 60 s
    }

    static async Task CheckPortable(Say say)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Taswib");
        var rel = await http.GetFromJsonAsync<Release>($"https://api.github.com/repos/{Links.Repo}/releases/latest");
        var latest = Version.Parse(rel!.tag_name.TrimStart('v'));
        if (latest <= Program.Version) { say.Info(T("uptodate")); return; }
        if (say.Silent) { say.Always(T("newver", latest.ToString(3)), 5000); return; } // background check: never pop a browser
        say.Info(T("newver_open", latest.ToString(3)));
        Process.Start(new ProcessStartInfo(rel.html_url) { UseShellExecute = true });
    }

    static bool IsOffline(Exception e)
    {
        for (var x = e; x != null; x = x.InnerException)
            if (x is HttpRequestException or System.Net.Sockets.SocketException) return true;
        return false;
    }

    // Started at login before Wi-Fi connected: try again in 30 min instead of waiting for the 4-hour check.
    void RetryLater()
    {
        if (retryPending) return;
        retryPending = true;
        TrayContext.After(30 * 60 * 1000, () => { retryPending = false; _ = Check(silent: true, restart: false); }); // no restart mid-use
    }

    // Where messages go: the Settings page if it asked, else the tray. Silent checks drop Info and Progress.
    sealed record Say(NotifyIcon Tray, Action<string>? Report, bool Silent)
    {
        public void Info(string msg, ToolTipIcon icon = ToolTipIcon.Info, int ms = 3000) { if (!Silent) Show(msg, icon, ms); }
        public void Always(string msg, int ms) => Show(msg, ToolTipIcon.Info, ms);
        public void Progress(string msg) { if (Silent) return; if (Report != null) Report(msg); else Tray.Text = msg; }
        void Show(string msg, ToolTipIcon icon, int ms) { if (Report != null) Report(msg); else Tray.ShowBalloonTip(ms, "Taswib", msg, icon); }
    }

    record Release(string tag_name, string html_url);
}
