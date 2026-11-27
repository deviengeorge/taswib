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
        List<(uint Format, byte[] Data)>? snap = null;
        var snapped = false;
        try
        {
            await ReleaseModifiers();
            var before = await HistoryIds();
            if (cfg.RestoreClipboard) { snap = Clip.Snapshot(); snapped = true; } // restore off: nothing to put back, skip the cost
            Clip.Clear();
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
                    Clip.SetText(output);
                    Press(VIRTUAL_KEY.VK_CONTROL, (VIRTUAL_KEY)'V');
                    await Task.Delay(200);
                    if (mode == Mode.Fix && cfg.SwitchLayout) SwitchTo(dir == Direction.ToArabic ? 0x01 : 0x09);
                    length = sel.Length;
                    result = Result.Fixed;
                }
                Log.Write($"{mode} scope={cfg.Scope} dir={dir} len={sel.Length} -> {result}"); // never the text itself
            }
            else Log.Write($"{mode} scope={cfg.Scope}: nothing copied");
            if (cfg.RestoreClipboard) Clip.Restore(snap);
            await Task.Delay(400); // history entries appear a moment after the copy
            await PurgeHistory(before);
        }
        catch (Exception e)
        {
            Log.Write("fix failed", e); result = Result.Failed; // e.g. clipboard locked by another app
            if (snapped) { try { Clip.Restore(snap); } catch (Exception e2) { Log.Write("restore after failure failed", e2); } } // best effort: don't leave the user's clipboard wiped
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

    /// <summary>Ctrl+C, then watch the clipboard sequence number: returns as soon as the copy lands.</summary>
    /// <returns>The copied text, or null if nothing was copied.</returns>
    // ponytail: no clipboard change within 250 ms = nothing selected; raise if slow apps get misread as empty.
    static async Task<string?> Copy()
    {
        var seq = Clip.Sequence;
        Press(VIRTUAL_KEY.VK_CONTROL, (VIRTUAL_KEY)'C');
        var start = Environment.TickCount64;
        while (true)
        {
            await Task.Delay(10);
            var ms = Environment.TickCount64 - start;
            if (Clip.Sequence != seq) { var t = Clip.GetText(); if (!string.IsNullOrEmpty(t)) return t; }
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
