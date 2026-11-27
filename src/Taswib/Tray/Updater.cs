using System.Diagnostics;
using System.Net.Http.Json;
using Velopack;
using Velopack.Sources;
using static Taswib.Strings;

namespace Taswib;

/// <summary>
/// Checks GitHub for a newer release.
/// Installed copy (Setup.exe): download it and restart into it. Portable exe: point the user at the release page.
/// </summary>
/// <param name="setTip">Shows download progress in the tray tooltip.</param>
sealed class Updater(Action<string> setTip)
{
    bool retryPending;

    /// <summary>Checks for an update and installs or announces it. Failures are logged and reported, never thrown.</summary>
    /// <param name="silent">Background check: only speaks when there is news.</param>
    /// <param name="restart">Restart into a downloaded update now (false = it applies on next start).</param>
    /// <param name="report">Settings page callback; without it messages go to the tray balloon.</param>
    public async Task Check(bool silent = false, bool restart = true, Action<string>? report = null)
    {
        var say = new Say(setTip, report, silent);
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
            say.Info(T(offline ? "updfail" : "updfail2"), warn: true, ms: 5000);
            if (silent && offline) RetryLater();
        }
    }

    /// <summary>Velopack manager for GitHub releases, or for a local folder when TASWIB_UPDATE_FEED is set (CI).</summary>
    static UpdateManager Manager()
    {
        var feed = Environment.GetEnvironmentVariable("TASWIB_UPDATE_FEED"); // CI update test serves releases from a local folder
        return string.IsNullOrEmpty(feed) ? new UpdateManager(new GithubSource($"https://github.com/{Links.Repo}", null, false)) : new UpdateManager(feed);
    }

    /// <summary>Installed copy: downloads the update with Velopack, then restarts into it or leaves it for next start.</summary>
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
        Log.Write($"update {version} downloaded");
        if (restart) { say.Info(T("restart")); mgr.ApplyUpdatesAndRestart(info); }
        else say.Always(T("ready", version), 5000); // Velopack applies it on next start; WaitExitThenApplyUpdates would kill the app after 60 s
    }

    /// <summary>Portable exe: compares with the latest GitHub release and opens its page (silent checks only notify).</summary>
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

    /// <summary>True if any exception in the chain is a network failure.</summary>
    static bool IsOffline(Exception e)
    {
        for (var x = e; x != null; x = x.InnerException)
            if (x is HttpRequestException or System.Net.Sockets.SocketException) return true;
        return false;
    }

    /// <summary>Started at login before Wi-Fi connected: try again in 30 min instead of waiting for the 4-hour check.</summary>
    void RetryLater()
    {
        if (retryPending) return;
        retryPending = true;
        TrayContext.After(30 * 60 * 1000, () => { retryPending = false; _ = Check(silent: true, restart: false); }); // no restart mid-use
    }

    /// <summary>Where messages go: the Settings page if it asked, else a notification. Silent checks drop Info and Progress.</summary>
    sealed record Say(Action<string> Tip, Action<string>? Report, bool Silent)
    {
        /// <summary>Shows a message, unless the check is silent.</summary>
        public void Info(string msg, bool warn = false, int ms = 3000) { if (!Silent) Show(msg, warn, ms); }
        /// <summary>Shows a message even on a silent check (news worth telling).</summary>
        public void Always(string msg, int ms) => Show(msg, false, ms);
        /// <summary>Shows download progress on the Settings page or tray tooltip (no notification), unless silent.</summary>
        public void Progress(string msg) { if (Silent) return; if (Report != null) Report(msg); else Tip(msg); }
        void Show(string msg, bool warn, int ms) { if (Report != null) Report(msg); else Toast.Show(msg, warn, ms); }
    }

    /// <summary>The fields used from GitHub's latest-release JSON (names match the API).</summary>
    record Release(string tag_name, string html_url);
}
