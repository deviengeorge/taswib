using System.Runtime.InteropServices;

namespace Taswib;

/// <summary>Hidden window that receives WM_HOTKEY for the global shortcuts (id 1 = Fix, id 2 = Clean).</summary>
sealed class HotkeyWindow : NativeWindow
{
    readonly Func<int, Task> onPress;
    /// <summary>Creates the hidden window; <paramref name="onPress"/> gets the hotkey id on each press.</summary>
    public HotkeyWindow(Func<int, Task> onPress) { this.onPress = onPress; CreateHandle(new CreateParams()); }
    /// <summary>Registers a global hotkey without auto-repeat. False if another app already owns it.</summary>
    public bool Register(int id, uint mod, ushort vk) => RegisterHotKey(Handle, id, mod | 0x4000, vk); // 0x4000 = no auto-repeat
    /// <summary>Releases both hotkeys (Fix and Clean).</summary>
    public void Unregister() { UnregisterHotKey(Handle, 1); UnregisterHotKey(Handle, 2); }
    /// <summary>Forwards WM_HOTKEY to the callback; everything else goes to the default handler.</summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) _ = onPress((int)m.WParam); // WM_HOTKEY, WParam = id
        else base.WndProc(ref m);
    }
    [DllImport("user32.dll")] static extern bool RegisterHotKey(nint h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint h, int id);
}
