using System.Runtime.InteropServices;

namespace Taswib;

// Hidden window that receives WM_HOTKEY for the global shortcuts (id 1 = Fix, id 2 = Clean).
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
