using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Taswib;

/// <summary>
/// Hidden message-only window that receives WM_HOTKEY for the global shortcuts (id 1 = Fix, id 2 = Clean).
/// Also owns our clipboard writes. Whatever message loop runs on this thread delivers its messages.
/// </summary>
sealed unsafe class HotkeyWindow
{
    static HotkeyWindow? current; // ponytail: one instance per process, so the static window procedure can find it
    readonly Func<int, Task> onPress;
    /// <summary>The window handle.</summary>
    public HWND Handle { get; }

    /// <summary>Creates the window; <paramref name="onPress"/> gets the hotkey id on each press.</summary>
    public HotkeyWindow(Func<int, Task> onPress)
    {
        this.onPress = onPress;
        current = this;
        var module = PInvoke.GetModuleHandle((char*)null);
        fixed (char* cls = "TaswibHotkeys")
        {
            var wc = new WNDCLASSEXW { cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(), lpfnWndProc = Proc, hInstance = module, lpszClassName = cls };
            PInvoke.RegisterClassEx(wc);
            Handle = PInvoke.CreateWindowEx(0, cls, null, 0, 0, 0, 0, 0, new HWND((void*)-3) /* HWND_MESSAGE */, default, module, null);
        }
        if (Handle.IsNull) throw new InvalidOperationException($"hotkey window not created ({Marshal.GetLastPInvokeError()})");
    }

    /// <summary>Registers a global hotkey without auto-repeat. False if another app already owns it.</summary>
    public bool Register(int id, uint mod, ushort vk) => PInvoke.RegisterHotKey(Handle, id, (HOT_KEY_MODIFIERS)(mod | 0x4000), vk); // 0x4000 = no auto-repeat
    /// <summary>Releases both hotkeys (Fix and Clean).</summary>
    public void Unregister() { PInvoke.UnregisterHotKey(Handle, 1); PInvoke.UnregisterHotKey(Handle, 2); }

    /// <summary>Forwards WM_HOTKEY to the callback; everything else goes to the default handler.</summary>
    static readonly WNDPROC Proc = WndProc; // kept in a static field so the GC never collects what Windows calls
    static LRESULT WndProc(HWND h, uint msg, WPARAM w, LPARAM l)
    {
        if (msg != 0x0312) return PInvoke.DefWindowProc(h, msg, w, l); // WM_HOTKEY, WParam = id
        try { _ = current?.onPress((int)w.Value); } catch (Exception e) { Log.Write("hotkey handler failed", e); } // an exception here would kill the process
        return default;
    }
}
