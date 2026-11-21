using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Taswib.Core;

namespace Taswib;

static class Program
{
    const string Repo = "deviengeorge/taswib";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "Taswib.SingleInstance", out var first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        Application.Run(new TrayContext());
    }

    sealed class TrayContext : ApplicationContext
    {
        readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Visible = true, Text = "Taswib" };
        readonly HotkeyWindow win;

        public TrayContext()
        {
            tray.ContextMenuStrip = new ContextMenuStrip();
            tray.ContextMenuStrip.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Fix text now", null, async (_, _) => await Fixer.Run()),
                new ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings()),
                new ToolStripMenuItem("Check for updates", null, async (_, _) => await CheckUpdate()),
                new ToolStripMenuItem("Exit", null, (_, _) => { tray.Visible = false; Application.Exit(); }),
            });
            tray.DoubleClick += (_, _) => ShowSettings();
            win = new HotkeyWindow(async () => await Fixer.Run());
            ApplyHotkey();
        }

        void ApplyHotkey()
        {
            var s = Settings.Current;
            win.Unregister();
            tray.Text = $"Taswib — {(s.Enabled ? s.Hotkey : "disabled")}";
            if (!s.Enabled) return;
            if (!Settings.TryParseHotkey(s.Hotkey, out var mod, out var key) || !win.Register(mod, key))
                tray.ShowBalloonTip(4000, "Taswib", $"Shortcut {s.Hotkey} is taken. Pick another in Settings.", ToolTipIcon.Warning);
        }

        void ShowSettings()
        {
            if (settingsOpen) return;
            settingsOpen = true;
            using var f = new SettingsForm(Settings.Current, IsStartup(), (s, startup) =>
            {
                s.Save(); SetStartup(startup); ApplyHotkey();
            });
            f.ShowDialog();
            settingsOpen = false;
        }
        bool settingsOpen;

        static bool IsStartup() => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue("Taswib") != null;

        static void SetStartup(bool on)
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true)!;
            if (on) k.SetValue("Taswib", $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue("Taswib", false);
        }

        async Task CheckUpdate()
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Taswib");
                var rel = await http.GetFromJsonAsync<Release>($"https://api.github.com/repos/{Repo}/releases/latest");
                var latest = Version.Parse(rel!.tag_name.TrimStart('v'));
                var mine = Assembly.GetExecutingAssembly().GetName().Version!;
                if (latest > mine) Process.Start(new ProcessStartInfo(rel.html_url) { UseShellExecute = true });
                else tray.ShowBalloonTip(3000, "Taswib", "You are up to date.", ToolTipIcon.Info);
            }
            catch { tray.ShowBalloonTip(3000, "Taswib", "Update check failed (offline?).", ToolTipIcon.Warning); }
        }

        record Release(string tag_name, string html_url);
    }

    sealed class HotkeyWindow : NativeWindow
    {
        readonly Func<Task> onPress;
        public HotkeyWindow(Func<Task> onPress) { this.onPress = onPress; CreateHandle(new CreateParams()); }
        public bool Register(uint mod, Keys k) => RegisterHotKey(Handle, 1, mod | 0x4000, (uint)k); // 0x4000 = no auto-repeat
        public void Unregister() => UnregisterHotKey(Handle, 1);
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) _ = onPress(); // WM_HOTKEY
            else base.WndProc(ref m);
        }
        [DllImport("user32.dll")] static extern bool RegisterHotKey(nint h, int id, uint mod, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint h, int id);
    }

    static class Fixer
    {
        static bool busy;

        public static async Task Run()
        {
            if (busy) return;
            busy = true;
            try
            {
                var cfg = Settings.Current;
                await ReleaseModifiers();
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
                    Clipboard.SetText(Mapper.Convert(sel, cfg.Direction));
                    Press(Keys.ControlKey, Keys.V);
                    await Task.Delay(200);
                }
                if (cfg.RestoreClipboard) { if (saved != null) Clipboard.SetText(saved); else Clipboard.Clear(); }
            }
            catch { /* clipboard locked by another app; user just presses again */ }
            finally { busy = false; }
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
