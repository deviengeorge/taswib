using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Taswib.Core;
using static Taswib.Strings;
using Velopack;
using Velopack.Sources;
using WinClip = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Taswib;

static class Program
{
    const string Repo = "deviengeorge/taswib";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [STAThread]
    static void Main()
    {
        VelopackApp.Build().Run(); // handles installer/update hooks, must be first
        using var mutex = new Mutex(true, "Taswib.SingleInstance", out var first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Log.Write("ui exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("fatal", (Exception)e.ExceptionObject);
        var v = Assembly.GetExecutingAssembly().GetName().Version!;
        Log.Write($"start {v.Major}.{v.Minor}.{v.Build} | {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        Application.Run(new TrayContext());
    }

    sealed class TrayContext : ApplicationContext
    {
        readonly NotifyIcon tray = new() { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!), Visible = true, Text = "Taswib" };
        readonly HotkeyWindow win;

        public TrayContext()
        {
            tray.ContextMenuStrip = new ContextMenuStrip();
            tray.DoubleClick += (_, _) => ShowSettings();
            win = new HotkeyWindow(async id => await RunFix(id == 1 ? Mode.Fix : Mode.Clean));
            ApplyHotkey();
            _ = CheckUpdate(silent: true);
            if (Settings.FirstRun) // show the welcome once the message loop is running
            {
                var t = new System.Windows.Forms.Timer { Interval = 800 };
                t.Tick += (_, _) => { t.Stop(); t.Dispose(); ShowWelcome(); };
                t.Start();
            }
        }

        void ShowWelcome()
        {
            Settings.Current.Save(); // creates settings.json, so the welcome never shows again
            using var w = new WelcomeForm(Settings.Current.Hotkey);
            w.ShowDialog();
            if (w.OpenSettings) ShowSettings();
        }

        async Task RunFix(Mode mode)
        {
            var r = await Fixer.Run(mode);
            switch (r)
            {
                case Result.Fixed: if (Settings.Current.SoundOnFix) System.Media.SystemSounds.Asterisk.Play(); break;
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
                new ToolStripMenuItem(T("fixnow"), null, async (_, _) => await RunFix(Mode.Fix)),
                new ToolStripMenuItem(T("cleannow"), null, async (_, _) => await RunFix(Mode.Clean)),
                new ToolStripMenuItem(T("settings"), null, (_, _) => ShowSettings()),
                new ToolStripMenuItem(T("update"), null, async (_, _) => await CheckUpdate()),
                new ToolStripMenuItem(T("exit"), null, (_, _) => { tray.Visible = false; Application.Exit(); }),
            });
        }

        void ApplyHotkey()
        {
            var s = Settings.Current;
            BuildMenu();
            win.Unregister();
            tray.Text = $"Taswib — {(s.Enabled ? s.Hotkey : T("disabled"))}";
            if (!s.Enabled || settingsOpen) return; // hotkeys stay released while Settings is open
            foreach (var (id, hk) in new[] { (1, s.Hotkey), (2, s.CleanHotkey) })
            {
                if (string.IsNullOrWhiteSpace(hk)) continue;
                if (!Settings.TryParseHotkey(hk, out var mod, out var key) || !win.Register(id, mod, key))
                { Log.Write($"hotkey {hk} not registered"); tray.ShowBalloonTip(4000, "Taswib", T("taken", hk), ToolTipIcon.Warning); }
            }
        }

        void ShowSettings()
        {
            if (settingsOpen) return;
            settingsOpen = true;
            win.Unregister(); // else the global hotkey swallows the key you are trying to record
            SettingsForm f;
            do
            {
                f = new SettingsForm(Settings.Current, IsStartup(), (s, startup) => { s.Save(); SetStartup(startup); ApplyHotkey(); }, () => _ = CheckUpdate());
                f.ShowDialog();
                f.Dispose();
            } while (f.Reopen);
            settingsOpen = false;
            ApplyHotkey();
        }
        bool settingsOpen;

        static bool IsStartup() => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue("Taswib") != null;

        static void SetStartup(bool on)
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true)!;
            if (on) k.SetValue("Taswib", $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue("Taswib", false);
        }

        // Installed (Setup.exe): download + restart into the new version. Portable exe: just open the release page.
        async Task CheckUpdate(bool silent = false)
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource($"https://github.com/{Repo}", null, false));
                if (mgr.IsInstalled)
                {
                    var info = await mgr.CheckForUpdatesAsync();
                    if (info == null) { if (!silent) tray.ShowBalloonTip(3000, "Taswib", T("uptodate"), ToolTipIcon.Info); return; }
                    tray.ShowBalloonTip(3000, "Taswib", T("updating", info.TargetFullRelease.Version), ToolTipIcon.Info);
                    await mgr.DownloadUpdatesAsync(info);
                    mgr.ApplyUpdatesAndRestart(info);
                    return;
                }
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Taswib");
                var rel = await http.GetFromJsonAsync<Release>($"https://api.github.com/repos/{Repo}/releases/latest");
                var latest = Version.Parse(rel!.tag_name.TrimStart('v'));
                var mine = Assembly.GetExecutingAssembly().GetName().Version!;
                if (latest > new Version(mine.Major, mine.Minor, mine.Build)) Process.Start(new ProcessStartInfo(rel.html_url) { UseShellExecute = true });
                else if (!silent) tray.ShowBalloonTip(3000, "Taswib", T("uptodate"), ToolTipIcon.Info);
            }
            catch (Exception e) { Log.Write("update check failed", e); if (!silent) tray.ShowBalloonTip(3000, "Taswib", T("updfail"), ToolTipIcon.Warning); }
        }

        record Release(string tag_name, string html_url);
    }

    sealed class HotkeyWindow : NativeWindow
    {
        readonly Func<int, Task> onPress;
        public HotkeyWindow(Func<int, Task> onPress) { this.onPress = onPress; CreateHandle(new CreateParams()); }
        public bool Register(int id, uint mod, Keys k) => RegisterHotKey(Handle, id, mod | 0x4000, (uint)k); // 0x4000 = no auto-repeat
        public void Unregister() { UnregisterHotKey(Handle, 1); UnregisterHotKey(Handle, 2); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) _ = onPress((int)m.WParam); // WM_HOTKEY, WParam = id
            else base.WndProc(ref m);
        }
        [DllImport("user32.dll")] static extern bool RegisterHotKey(nint h, int id, uint mod, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint h, int id);
    }

    enum Mode { Fix, Clean }
    enum Result { Fixed, NothingSelected, NoChange, Failed, Busy }

    static class Fixer
    {
        static bool busy;

        public static async Task<Result> Run(Mode mode)
        {
            if (busy) return Result.Busy;
            busy = true;
            var result = Result.NothingSelected;
            try
            {
                var cfg = Settings.Current;
                await ReleaseModifiers();
                var before = await HistoryIds();
                var saved = GetText();
                Clipboard.Clear();
                Press(Keys.ControlKey, Keys.C);
                var sel = await Wait();
                if (sel == null && cfg.Scope != Scope.SelectionOnly)
                {
                    if (cfg.Scope == Scope.SelectionOrLine) Press(Keys.ShiftKey, Keys.Home);
                    else Press(Keys.ControlKey, Keys.A);
                    Press(Keys.ControlKey, Keys.C);
                    sel = await Wait();
                }
                if (sel != null)
                {
                    var dir = Mapper.Resolve(sel, cfg.Direction);
                    var output = sel;
                    if (mode == Mode.Fix) output = Mapper.Convert(sel, dir);
                    else
                    {
                        if (cfg.CleanPunctuation) output = TextTools.FixPunctuation(output);
                        if (cfg.CleanNormalize) output = TextTools.Normalize(output);
                    }
                    if (output == sel) result = Result.NoChange;
                    else
                    {
                        SetPrivate(output);
                        Press(Keys.ControlKey, Keys.V);
                        await Task.Delay(200);
                        if (mode == Mode.Fix && cfg.SwitchLayout) SwitchTo(dir == Direction.ToArabic ? 0x01 : 0x09);
                        result = Result.Fixed;
                    }
                    Log.Write($"{mode} scope={cfg.Scope} dir={dir} len={sel.Length} -> {result}"); // never the text itself
                }
                else Log.Write($"{mode} scope={cfg.Scope}: nothing copied");
                if (cfg.RestoreClipboard) { if (saved != null) SetPrivate(saved); else Clipboard.Clear(); }
                await Task.Delay(400); // history entries appear a moment after the copy
                await PurgeHistory(before);
            }
            catch (Exception e) { Log.Write("fix failed", e); result = Result.Failed; } // e.g. clipboard locked by another app
            finally { busy = false; }
            return result;
        }

        // Ask the focused window to use an installed layout whose primary language is `lang` (0x01 Arabic, 0x09 English).
        static void SwitchTo(int lang)
        {
            var n = GetKeyboardLayoutList(0, null);
            var list = new nint[n];
            GetKeyboardLayoutList(n, list);
            var hkl = list.FirstOrDefault(h => ((int)h & 0x3FF) == lang);
            if (hkl != 0) PostMessage(GetForegroundWindow(), 0x0050, 0, hkl); // WM_INPUTLANGCHANGEREQUEST
        }
        [DllImport("user32.dll")] static extern int GetKeyboardLayoutList(int n, nint[]? list);
        [DllImport("user32.dll")] static extern nint GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool PostMessage(nint h, uint msg, nint w, nint l);

        // Our own clipboard writes are hidden from Win+V history and cloud sync.
        static void SetPrivate(string text)
        {
            var d = new DataObject();
            d.SetText(text);
            foreach (var f in new[] { "ExcludeClipboardContentFromMonitorProcessing", "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" })
                d.SetData(f, new MemoryStream(new byte[4]));
            Clipboard.SetDataObject(d, true);
        }

        // The app's own Ctrl+C lands in Win+V history; remove whatever appeared during our run.
        static async Task<HashSet<string>?> HistoryIds()
        {
            try
            {
                if (!WinClip.IsHistoryEnabled()) return null;
                var r = await WinClip.GetHistoryItemsAsync();
                return r.Status == Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.Success ? r.Items.Select(i => i.Id).ToHashSet() : null;
            }
            catch (Exception e) { Log.Write("history read failed", e); return null; }
        }

        static async Task PurgeHistory(HashSet<string>? before)
        {
            if (before == null) return;
            try
            {
                var r = await WinClip.GetHistoryItemsAsync();
                foreach (var i in r.Items.Where(i => !before.Contains(i.Id))) WinClip.DeleteItemFromHistory(i);
            }
            catch (Exception e) { Log.Write("history purge failed", e); }
        }

        static string? GetText() => Clipboard.ContainsText() ? Clipboard.GetText() : null;

        static async Task<string?> Wait()
        {
            for (var i = 0; i < 10; i++) { await Task.Delay(30); var t = GetText(); if (!string.IsNullOrEmpty(t)) return t; }
            return null;
        }

        // Shortcut modifiers may still be physically down; wait so our Ctrl+C isn't mangled (max ~1s).
        static async Task ReleaseModifiers()
        {
            for (var i = 0; i < 50 && new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); i++)
                await Task.Delay(20);
        }
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

        static void Press(Keys mod, Keys key)
        {
            Send(mod, false); Send(key, false); Send(key, true); Send(mod, true);
        }

        static void Send(Keys k, bool up)
        {
            var i = new INPUT { type = 1, ki = new KEYBDINPUT { wVk = (ushort)k, dwFlags = up ? 2u : 0u } };
            SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>());
        }

        [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] i, int size);
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public KEYBDINPUT ki; long pad; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public nint extra; }
    }
}
