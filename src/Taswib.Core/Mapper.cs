using System.Text;

namespace Taswib.Core;

public enum Direction { Auto, ToArabic, ToEnglish }

/// <summary>US QWERTY &lt;-&gt; Arabic 101 physical-key mapping. Pure, no Windows deps.</summary>
public static class Mapper
{
    // Same key order in both pairs. Arabic tokens are space-separated (some keys give 2 chars: لا).
    const string EnPlain = "`qwertyuiop[]asdfghjkl;'zxcvbnm,./";
    const string ArPlain = "ذ ض ص ث ق ف غ ع ه خ ح ج د ش س ي ب ل ا ت ن م ك ط ئ ء ؤ ر لا ى ة و ز ظ";
    const string EnShift = "~QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?";
    const string ArShift = "ّ َ ً ُ ٌ لإ إ ‘ ÷ × ؛ < > ِ ٍ ] [ لأ أ ـ ، / : \" ~ ْ } { لآ آ ’ , . ؟";

    static readonly Dictionary<char, string> ToAr = new();
    static readonly Dictionary<char, char> ToEn = new();
    static readonly Dictionary<string, char> PairToEn = new(); // two-char keys: لا -> b, لأ -> G ...

    static Mapper()
    {
        Add(EnPlain, ArPlain);
        Add(EnShift, ArShift);
        for (var i = 0; i < 10; i++) ToEn[(char)('٠' + i)] = (char)('0' + i); // Arabic-Indic digits
    }

    static void Add(string en, string arTokens)
    {
        var ar = arTokens.Split(' ');
        if (ar.Length != en.Length) throw new InvalidOperationException("layout tables out of sync");
        for (var i = 0; i < en.Length; i++)
        {
            ToAr.TryAdd(en[i], ar[i]);
            // reverse: only single non-ASCII chars, so real ASCII punctuation in Arabic text stays put
            if (ar[i].Length == 1 && ar[i][0] > 127) ToEn.TryAdd(ar[i][0], en[i]);
            else if (ar[i].Length == 2) PairToEn.TryAdd(ar[i], en[i]);
        }
    }

    public static bool IsArabic(char c) => c >= '؀' && c <= 'ۿ';

    /// <summary>Which way a conversion will go: forced by mode, else by which script dominates.</summary>
    public static Direction Resolve(string text, Direction mode = Direction.Auto)
    {
        if (mode != Direction.Auto) return mode;
        var ar = text.Count(IsArabic);
        var lat = text.Count(c => c < 128 && char.IsLetter(c));
        return ar > lat ? Direction.ToEnglish : Direction.ToArabic;
    }

    /// <param name="capsLock">Caps Lock was on while typing: English letters came out inverted, so flip their case before mapping to Arabic.</param>
    public static string Convert(string text, Direction mode = Direction.Auto, bool capsLock = false)
    {
        var toEn = Resolve(text, mode) == Direction.ToEnglish;
        if (capsLock && !toEn)
            text = string.Concat(text.Select(c => c < 128 && char.IsLetter(c) ? (char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c)) : c));
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (!toEn) { sb.Append(ToAr.TryGetValue(c, out var a) ? a : c); continue; }
            if (i + 1 < text.Length && PairToEn.TryGetValue(text.Substring(i, 2), out var k))
            {
                sb.Append(k == 'b' && LooksLikeGh(sb, text, i + 2) ? "gh" : k.ToString());
                i++;
                continue;
            }
            sb.Append(ToEn.TryGetValue(c, out var e) ? e : c);
        }
        return sb.ToString();
    }

    // The b key types ل+ا, exactly what g then h type, so "about" and "high" look alike in Arabic.
    // ponytail: spelling guess from the letters around it; misses "doubt", "neighbor", "ghost". Add a word list if users hit these.
    static bool LooksLikeGh(StringBuilder done, string text, int next)
    {
        char Before(int back) => done.Length >= back ? char.ToLowerInvariant(done[^back]) : ' ';
        char After(int j) => j < text.Length && ToEn.TryGetValue(text[j], out var e) ? char.ToLowerInvariant(e) : ' '; // space, digit, punctuation = word end
        var prev2 = $"{Before(2)}{Before(1)}";
        var n = After(next);
        if (n == 't') return Before(1) == 'i' || prev2 is "ou" or "au";   // right, night, thought, caught
        if (n is ' ' or 's' || (n == 'l' && After(next + 1) == 'y'))       // high, laughs, roughly
            return prev2 is "hi" or "si" or "ni" or "ei" or "ou" or "au";  // but club, rib, possible stay b
        return false;
    }
}
