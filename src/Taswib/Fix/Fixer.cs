using System.Runtime.InteropServices;
using Taswib.Core;
using WinClip = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Taswib;

enum Mode { Fix, Clean }
enum Result { Fixed, NothingSelected, NoChange, Failed, Busy }

// The fix flow: copy the selection, convert it, paste it back, then restore the clipboard and hide our traces.
static class Fixer
{
    static bool busy;

    // Returns the outcome and the length of the text it fixed (0 unless Fixed). Settings come in as a parameter, not read globally.
    public static async Task<(Result Result, int Length)> Run(Mode mode, Settings cfg)
    {
        if (busy) return (Result.Busy, 0);
        busy = true;
        var result = Result.NothingSelected;
        var length = 0;
        Dictionary<string, object>? snap = null;
        var snapped = false;
        try
        {
            await ReleaseModifiers();
            var before = await HistoryIds();
            if (cfg.RestoreClipboard) { snap = Snapshot(); snapped = true; } // restore off: nothing to put back, skip the cost
            Clipboard.Clear();
            var sel = await Copy();
            if (sel == null && cfg.Scope != Scope.SelectionOnly)
            {
                if (cfg.Scope == Scope.SelectionOrLine) Press(Keys.ShiftKey, Keys.Home);
                else Press(Keys.ControlKey, Keys.A);
                sel = await Copy();
            }
            if (sel != null)
            {
                var dir = Mapper.Resolve(sel, cfg.Direction);
                var output = sel;
                if (mode == Mode.Fix) output = Mapper.Convert(sel, dir, Control.IsKeyLocked(Keys.CapsLock));
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
                    length = sel.Length;
                    result = Result.Fixed;
                }
                Log.Write($"{mode} scope={cfg.Scope} dir={dir} len={sel.Length} -> {result}"); // never the text itself
            }
            else Log.Write($"{mode} scope={cfg.Scope}: nothing copied");
            if (cfg.RestoreClipboard) Restore(snap);
            await Task.Delay(400); // history entries appear a moment after the copy
            await PurgeHistory(before);
        }
        catch (Exception e)
        {
            Log.Write("fix failed", e); result = Result.Failed; // e.g. clipboard locked by another app
            if (snapped) { try { Restore(snap); } catch (Exception e2) { Log.Write("restore after failure failed", e2); } } // best effort: don't leave the user's clipboard wiped
        }
        finally { busy = false; }
        return (result, length);
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

    static readonly string[] OleFormats = { "Embed Source", "Link Source", "Link Source Descriptor", "Object Descriptor", "Ole Private Data" };

    static Dictionary<string, object>? Snapshot()
    {
        try
        {
            var d = Clipboard.GetDataObject();
            if (d == null) return null;
            var formats = d.GetFormats(false);
            bool Has(string f) => Array.IndexOf(formats, f) >= 0;
            var snap = new Dictionary<string, object>();
            var start = Environment.TickCount64;
            foreach (var f in formats)
            {
                if (Array.IndexOf(OleFormats, f) >= 0) continue; // live links to the source app: useless once restored
                if (f == DataFormats.Bitmap && Has(DataFormats.Dib)) continue; // Windows rebuilds these from their sibling
                if (f == DataFormats.MetafilePict && Has(DataFormats.EnhancedMetafile)) continue;
                if ((f == DataFormats.Text || f == DataFormats.OemText) && Has(DataFormats.UnicodeText)) continue;
                // ponytail: 500 ms budget, checked between formats (one slow format can still block). Apps list richest formats first, so a cut loses only extras.
                if (Environment.TickCount64 - start > 500) { Log.Write($"clipboard snapshot cut at {snap.Count}/{formats.Length} formats"); break; }
                try { if (d.GetData(f, false) is { } v) snap[f] = v; } catch { /* a format that can't be read is skipped */ }
            }
            return snap;
        }
        catch (Exception e) { Log.Write("clipboard snapshot failed", e); return null; }
    }

    static void Restore(Dictionary<string, object>? snap)
    {
        if (snap == null || snap.Count == 0) { Clipboard.Clear(); return; }
        var d = new DataObject();
        foreach (var (f, v) in snap) { try { d.SetData(f, false, v); } catch { } }
        SetHidden(d);
    }

    static void SetPrivate(string text)
    {
        var d = new DataObject();
        d.SetText(text);
        SetHidden(d);
    }

    // Our own clipboard writes are hidden from Win+V history and cloud sync.
    static void SetHidden(DataObject d)
    {
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

    // Ctrl+C, then watch the clipboard sequence number: returns as soon as the copy lands.
    // ponytail: no clipboard change within 250 ms = nothing selected; raise if slow apps get misread as empty.
    static async Task<string?> Copy()
    {
        var seq = GetClipboardSequenceNumber();
        Press(Keys.ControlKey, Keys.C);
        var start = Environment.TickCount64;
        while (true)
        {
            await Task.Delay(10);
            var ms = Environment.TickCount64 - start;
            if (GetClipboardSequenceNumber() != seq) { var t = GetText(); if (!string.IsNullOrEmpty(t)) return t; }
            else if (ms >= 250) return null;
            if (ms >= 600) return null;
        }
    }
    [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();

    // Shortcut modifiers may still be physically down; wait so our Ctrl+C isn't mangled (max ~1s).
    static async Task ReleaseModifiers()
    {
        for (var i = 0; i < 50 && new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0); i++)
            await Task.Delay(20);
    }
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);

    // One SendInput call: the 4 events are atomic, so keys the user types can't land in between.
    static void Press(Keys mod, Keys key)
    {
        var i = new[] { Key(mod, false), Key(key, false), Key(key, true), Key(mod, true) };
        SendInput((uint)i.Length, i, Marshal.SizeOf<INPUT>());
    }

    // Without KEYEVENTF_EXTENDEDKEY (1), Home etc. are read as the numpad key, and with NumLock on Shift+Numpad7 moves without selecting.
    static readonly Keys[] Extended = { Keys.Home, Keys.End, Keys.Insert, Keys.Delete, Keys.PageUp, Keys.PageDown, Keys.Left, Keys.Right, Keys.Up, Keys.Down };
    static INPUT Key(Keys k, bool up) =>
        new() { type = 1, ki = new KEYBDINPUT { wVk = (ushort)k, dwFlags = (up ? 2u : 0u) | (Array.IndexOf(Extended, k) >= 0 ? 1u : 0u) } };

    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] i, int size);
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public KEYBDINPUT ki; long pad; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public nint extra; }
}
