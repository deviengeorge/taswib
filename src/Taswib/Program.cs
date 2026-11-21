using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Taswib.Core;

namespace Taswib;

static class Program
{
    // ponytail: hotkey is a constant. Make it configurable when someone asks.
    const Keys Hotkey = Keys.Pause;
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
        readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Visible = true, Text = "Taswib — Pause to fix text" };
        readonly HotkeyWindow win;

        public TrayContext()
        {
            var startup = new ToolStripMenuItem("Start with Windows") { Checked = IsStartup(), CheckOnClick = true };
            startup.CheckedChanged += (_, _) => SetStartup(startup.Checked);
            tray.ContextMenuStrip = new ContextMenuStrip();
            tray.ContextMenuStrip.Items.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("Fix text now (Pause)", null, async (_, _) => await Fixer.Run()),
                startup,
                new ToolStripMenuItem("Check for updates", null, async (_, _) => await CheckUpdate()),
                new ToolStripMenuItem("Exit", null, (_, _) => { tray.Visible = false; Application.Exit(); }),
            });
            win = new HotkeyWindow(async () => await Fixer.Run());
            if (!win.Register(Hotkey))
                tray.ShowBalloonTip(4000, "Taswib", "Pause key is taken by another program.", ToolTipIcon.Warning);
        }

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
        public bool Register(Keys k) => RegisterHotKey(Handle, 1, 0, (uint)k);
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312) _ = onPress(); // WM_HOTKEY
            else base.WndProc(ref m);
        }
        [DllImport("user32.dll")] static extern bool RegisterHotKey(nint h, int id, uint mod, uint vk);
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
                var saved = GetText();
                Clipboard.Clear();
                Press(Keys.ControlKey, Keys.C);
                var sel = await Wait();
                if (sel == null) // nothing selected: take the line before the caret
                {
                    Press(Keys.ShiftKey, Keys.Home);
                    Press(Keys.ControlKey, Keys.C);
                    sel = await Wait();
                }
                if (sel != null)
                {
                    Clipboard.SetText(Mapper.Convert(sel));
                    Press(Keys.ControlKey, Keys.V);
                    await Task.Delay(200);
                }
                if (saved != null) Clipboard.SetText(saved); else Clipboard.Clear();
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

        // Pause was physically pressed; modifiers are not held, so a plain chord is safe.
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
