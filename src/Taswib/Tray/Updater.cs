using System.Diagnostics;
using System.Net.Http.Json;
using Velopack;
using Velopack.Sources;
using static Taswib.Strings;

namespace Taswib;

// Update check, split out of TrayContext. Reports through the tray balloon unless a Settings callback is given.
sealed class Updater(NotifyIcon tray)
{
    // Installed (Setup.exe): download + restart into the new version. Portable exe: just open the release page.
    // report != null (Settings button): show the result there; otherwise as a tray balloon.
    bool retryPending;
    public async Task Check(bool silent = false, bool restart = true, Action<string>? report = null)
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
                TrayContext.After(30 * 60 * 1000, () => { retryPending = false; _ = Check(silent: true, restart: false); }); // no restart mid-use
            }
        }
    }

    record Release(string tag_name, string html_url);
}
