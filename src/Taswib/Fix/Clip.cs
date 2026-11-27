using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace Taswib;

/// <summary>
/// Raw Win32 clipboard: read and write any format as bytes, so a snapshot can be put back exactly.
/// Our own writes carry the formats that keep them out of Win+V history and cloud sync.
/// </summary>
static class Clip
{
    /// <summary>Window that owns our clipboard writes. Without one, SetClipboardData fails after EmptyClipboard.</summary>
    public static HWND Owner;

    const uint CF_TEXT = 1, CF_OEMTEXT = 7, CF_UNICODETEXT = 13, CF_DIB = 8, CF_DIBV5 = 17;
    /// <summary>Formats whose handle is a GDI object or a live link, not a block of bytes: cannot be copied, so skipped.</summary>
    static readonly uint[] NotBytes = { 2 /*BITMAP*/, 3 /*METAFILEPICT*/, 9 /*PALETTE*/, 14 /*ENHMETAFILE*/, 0x80 /*OWNERDISPLAY*/, 0x82, 0x83, 0x8E /*DSP*/ };
    static readonly string[] OleFormats = { "Embed Source", "Link Source", "Link Source Descriptor", "Object Descriptor", "Ole Private Data" };
    static readonly string[] HideFormats = { "ExcludeClipboardContentFromMonitorProcessing", "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" };

    /// <summary>Changes on every clipboard write by any app.</summary>
    public static uint Sequence => PInvoke.GetClipboardSequenceNumber();

    /// <summary>The clipboard's Unicode text, or null if it holds none.</summary>
    public static unsafe string? GetText()
    {
        if (!PInvoke.IsClipboardFormatAvailable(CF_UNICODETEXT)) return null;
        Open();
        try
        {
            var h = (HGLOBAL)(nint)PInvoke.GetClipboardData(CF_UNICODETEXT).Value;
            var p = PInvoke.GlobalLock(h);
            if (p == null) return null;
            try { return Marshal.PtrToStringUni((nint)p); }
            finally { PInvoke.GlobalUnlock(h); }
        }
        finally { PInvoke.CloseClipboard(); }
    }

    /// <summary>Empties the clipboard.</summary>
    public static void Clear() { Open(); try { PInvoke.EmptyClipboard(); } finally { PInvoke.CloseClipboard(); } }

    /// <summary>Puts text on the clipboard, hidden from history and cloud sync.</summary>
    public static void SetText(string text) =>
        Write(new() { (CF_UNICODETEXT, System.Text.Encoding.Unicode.GetBytes(text + "\0")) });

    /// <summary>Puts a snapshot back (hidden from history); null or empty just clears the clipboard.</summary>
    public static void Restore(List<(uint Format, byte[] Data)>? snap)
    {
        if (snap == null || snap.Count == 0) Clear();
        else Write(snap);
    }

    /// <summary>
    /// Copies every format that is a plain block of bytes. Skips GDI handles, OLE links and text forms Windows
    /// rebuilds from Unicode text; stops after a 500 ms budget.
    /// </summary>
    /// <returns>Format id and bytes per format, or null if the clipboard was empty or unreadable (logged).</returns>
    // ponytail: bitmap-only and metafile-only content is lost (GDI handles); Windows offers a DIB for bitmaps, which is kept.
    public static List<(uint Format, byte[] Data)>? Snapshot()
    {
        try { Open(); }
        catch (Exception e) { Log.Write("clipboard snapshot failed", e); return null; }
        try
        {
            var formats = new List<uint>();
            for (uint f = 0; (f = PInvoke.EnumClipboardFormats(f)) != 0;) formats.Add(f);
            if (formats.Count == 0) return null;
            var snap = new List<(uint, byte[])>();
            var start = Environment.TickCount64;
            foreach (var f in formats)
            {
                if (Array.IndexOf(NotBytes, f) >= 0 || f is >= 0x200 and <= 0x3FF) continue; // private and GDI-object ranges
                if (f is CF_TEXT or CF_OEMTEXT && formats.Contains(CF_UNICODETEXT)) continue;
                if (f == CF_DIBV5 && formats.Contains(CF_DIB)) continue;
                if (Array.IndexOf(OleFormats, Name(f)) >= 0) continue; // live links to the source app: useless once restored
                if (Environment.TickCount64 - start > 500) { Log.Write($"clipboard snapshot cut at {snap.Count}/{formats.Count} formats"); break; }
                if (Read(f) is { } d) snap.Add((f, d));
            }
            return snap;
        }
        finally { PInvoke.CloseClipboard(); }
    }

    /// <summary>Opens the clipboard, retrying briefly while another app holds it.</summary>
    static void Open()
    {
        for (var i = 0; !PInvoke.OpenClipboard(Owner); i++)
        {
            if (i == 20) throw new IOException("clipboard is busy");
            Thread.Sleep(10);
        }
    }

    /// <summary>Registered name of a custom format ("" for standard ones).</summary>
    static unsafe string Name(uint f)
    {
        var buf = stackalloc char[128];
        var n = PInvoke.GetClipboardFormatName(f, buf, 128);
        return new string(buf, 0, n);
    }

    /// <summary>Copies one format's bytes; null if it can't be read.</summary>
    static unsafe byte[]? Read(uint f)
    {
        var h = (HGLOBAL)(nint)PInvoke.GetClipboardData(f).Value;
        if (h.Value == null) return null;
        var size = (int)PInvoke.GlobalSize(h);
        var p = PInvoke.GlobalLock(h);
        if (p == null || size == 0) return null;
        try { return new ReadOnlySpan<byte>(p, size).ToArray(); }
        finally { PInvoke.GlobalUnlock(h); }
    }

    /// <summary>Replaces the clipboard with these formats plus the "keep out of history" markers.</summary>
    static void Write(List<(uint Format, byte[] Data)> items)
    {
        Open();
        try
        {
            PInvoke.EmptyClipboard();
            foreach (var (f, d) in items) Put(f, d);
            foreach (var name in HideFormats) Put(PInvoke.RegisterClipboardFormat(name), new byte[4]); // DWORD 0 = no
        }
        finally { PInvoke.CloseClipboard(); }
    }

    /// <summary>Copies bytes into a movable global block and hands it to the clipboard (which then owns it).</summary>
    static unsafe void Put(uint f, byte[] data)
    {
        var h = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)data.Length);
        if (h.Value == null) throw new OutOfMemoryException("GlobalAlloc failed");
        var p = PInvoke.GlobalLock(h);
        data.CopyTo(new Span<byte>(p, data.Length));
        PInvoke.GlobalUnlock(h);
        if (PInvoke.SetClipboardData(f, (HANDLE)(nint)h.Value).Value == null) PInvoke.GlobalFree(h); // not taken: still ours to free
    }
}
