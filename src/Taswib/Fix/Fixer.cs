using Taswib.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using WinClip = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Taswib;

/// <summary>Fix converts the keyboard layout; Clean fixes punctuation and normalizes letters.</summary>
enum Mode { Fix, Clean }
/// <summary>Outcome of one hotkey run. Busy means a previous run was still going.</summary>
enum Result { Fixed, NothingSelected, NoChange, Failed, Busy }

/// <summary>The fix flow: copy the selection, convert it, paste it back, then restore the clipboard and hide our traces.</summary>
static class Fixer
{
    static bool busy;

    /// <summary>
    /// Runs one Fix or Clean on the focused app's selection (or line/all per <c>cfg.Scope</c>).
    /// Settings come in as a parameter, not read globally. Only one run at a time.
    /// </summary>
    /// <returns>The outcome and the length of the text it fixed (0 unless Fixed).</returns>
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
                if (cfg.Scope == Scope.SelectionOrLine) Press(VIRTUAL_KEY.VK_SHIFT, VIRTUAL_KEY.VK_HOME);
                else Press(VIRTUAL_KEY.VK_CONTROL, (VIRTUAL_KEY)'A');
                sel = await Copy();
            }
            if (sel != null)
            {
                var dir = Mapper.Resolve(sel, cfg.Direction);
                var output = sel;
                if (mode == Mode.Fix) output = Mapper.Convert(sel, dir, (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 1) != 0);
                else
                {
                    if (cfg.CleanPunctuation) output = TextTools.FixPunctuation(output);
                    if (cfg.CleanNormalize) output = TextTools.Normalize(output);
                }
                if (output == sel) result = Result.NoChange;
                else
                {
                    SetPrivate(output);
                    Press(VIRTUAL_KEY.VK_CONTROL, (VIRTUAL_KEY)'V');
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

    /// <summary>Ask the focused window to use an installed layout whose primary language is <paramref name="lang"/> (0x01 Arabic, 0x09 English).</summary>
    static void SwitchTo(int lang)
    {
        var hkl = Win.Layouts().FirstOrDefault(h => ((int)h & 0x3FF) == lang);
        if (hkl != 0) PInvoke.PostMessage(PInvoke.GetForegroundWindow(), 0x0050, 0, hkl); // WM_INPUTLANGCHANGEREQUEST
    }

    static readonly string[] OleFormats = { "Embed Source", "Link Source", "Link Source Descriptor", "Object Descriptor", "Ole Private Data" };

    /// <summary>
    /// Copies every readable clipboard format so it can be restored later. Skips OLE link formats and
    /// ones Windows rebuilds from a sibling; stops after a 500 ms budget.
    /// </summary>
    /// <returns>Format name to data, or null if the clipboard was empty or unreadable.</returns>
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

    /// <summary>Puts a snapshot back on the clipboard (hidden from history); an empty snapshot just clears it.</summary>
    static void Restore(Dictionary<string, object>? snap)
    {
        if (snap == null || snap.Count == 0) { Clipboard.Clear(); return; }
        var d = new DataObject();
        foreach (var (f, v) in snap) { try { d.SetData(f, false, v); } catch { } }
        SetHidden(d);
    }

    /// <summary>Puts the converted text on the clipboard, hidden from history and cloud sync.</summary>
    static void SetPrivate(string text)
    {
        var d = new DataObject();
        d.SetText(text);
        SetHidden(d);
    }

    /// <summary>Our own clipboard writes are hidden from Win+V history and cloud sync.</summary>
    static void SetHidden(DataObject d)
    {
        foreach (var f in new[] { "ExcludeClipboardContentFromMonitorProcessing", "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" })
            d.SetData(f, new MemoryStream(new byte[4]));
        Clipboard.SetDataObject(d, true);
    }

    /// <summary>
    /// The app's own Ctrl+C lands in Win+V history; remove whatever appeared during our run.
    /// This records the item ids before the run.
    /// </summary>
    /// <returns>Current history ids, or null if history is off or unreadable (then nothing is purged).</returns>
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

    /// <summary>Deletes Win+V history items that were not in <paramref name="before"/>, i.e. our own copies.</summary>
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

    /// <summary>Ctrl+C, then watch the clipboard sequence number: returns as soon as the copy lands.</summary>
    /// <returns>The copied text, or null if nothing was copied.</returns>
    // ponytail: no clipboard change within 250 ms = nothing selected; raise if slow apps get misread as empty.
    static async Task<string?> Copy()
    {
        var seq = PInvoke.GetClipboardSequenceNumber();
        Press(VIRTUAL_KEY.VK_CONTROL, (VIRTUAL_KEY)'C');
        var start = Environment.TickCount64;
        while (true)
        {
            await Task.Delay(10);
            var ms = Environment.TickCount64 - start;
            if (PInvoke.GetClipboardSequenceNumber() != seq) { var t = GetText(); if (!string.IsNullOrEmpty(t)) return t; }
            else if (ms >= 250) return null;
            if (ms >= 600) return null;
        }
    }

    /// <summary>Shortcut modifiers may still be physically down; wait so our Ctrl+C isn't mangled (max ~1s).</summary>
    static async Task ReleaseModifiers()
    {
        for (var i = 0; i < 50 && new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (PInvoke.GetAsyncKeyState(k) & 0x8000) != 0); i++)
            await Task.Delay(20);
    }

    /// <summary>Sends mod+key down/up in one SendInput call: atomic, so keys the user types can't land in between.</summary>
    static void Press(VIRTUAL_KEY mod, VIRTUAL_KEY key)
    {
        var i = new[] { Key(mod, false), Key(key, false), Key(key, true), Key(mod, true) };
        PInvoke.SendInput(i, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    // Without KEYEVENTF_EXTENDEDKEY (1), Home etc. are read as the numpad key, and with NumLock on Shift+Numpad7 moves without selecting.
    static readonly VIRTUAL_KEY[] Extended = { VIRTUAL_KEY.VK_HOME, VIRTUAL_KEY.VK_END, VIRTUAL_KEY.VK_INSERT, VIRTUAL_KEY.VK_DELETE, VIRTUAL_KEY.VK_PRIOR, VIRTUAL_KEY.VK_NEXT, VIRTUAL_KEY.VK_LEFT, VIRTUAL_KEY.VK_RIGHT, VIRTUAL_KEY.VK_UP, VIRTUAL_KEY.VK_DOWN };
    /// <summary>Builds one keyboard INPUT event, flagged extended for navigation keys.</summary>
    static INPUT Key(VIRTUAL_KEY k, bool up) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = { ki = new KEYBDINPUT { wVk = k, dwFlags = (up ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP : 0) | (Array.IndexOf(Extended, k) >= 0 ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0) } },
    };
}
