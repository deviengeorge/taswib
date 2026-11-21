using System.Text;

namespace Taswib.Core;

public static class TextTools
{
    /// <summary>, ? ; become ، ؟ ؛ when the previous visible character is Arabic.</summary>
    public static string FixPunctuation(string text)
    {
        var sb = new StringBuilder(text.Length);
        var prev = ' ';
        foreach (var c in text)
        {
            var r = Mapper.IsArabic(prev) ? c switch { ',' => '،', '?' => '؟', ';' => '؛', _ => c } : c;
            sb.Append(r);
            if (!char.IsWhiteSpace(c)) prev = r;
        }
        return sb.ToString();
    }

    /// <summary>أ إ آ ٱ → ا, ى → ي, and removes tashkeel and tatweel. Does not touch ة/ه.</summary>
    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == 'ـ' || c == 'ٰ' || (c >= 'ً' && c <= 'ْ')) continue;
            sb.Append(c switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ى' => 'ي', _ => c });
        }
        return sb.ToString();
    }
}
