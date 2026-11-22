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
        foreach (var c in text)
        {
            if (toEn) sb.Append(ToEn.TryGetValue(c, out var e) ? e : c);
            else sb.Append(ToAr.TryGetValue(c, out var a) ? a : c);
        }
        return sb.ToString();
    }
}
