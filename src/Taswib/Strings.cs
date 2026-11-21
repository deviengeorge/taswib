namespace Taswib;

// UI text. Arabic is Egyptian, not MSA.
static class Strings
{
    static readonly Dictionary<string, (string En, string Ar)> T_ = new()
    {
        ["fixnow"] = ("Fix text now", "صلّح الكلام دلوقتي"),
        ["cleannow"] = ("Clean text now", "نضّف الكلام دلوقتي"),
        ["settings"] = ("Settings…", "الإعدادات…"),
        ["update"] = ("Check for updates", "دوّر على تحديث"),
        ["exit"] = ("Exit", "خروج"),
        ["title"] = ("Taswib settings", "إعدادات تصويب"),
        ["hotkey"] = ("Fix shortcut (click, press keys)", "اختصار التصليح (دوس واضغط)"),
        ["cleankey"] = ("Clean shortcut (click, press keys)", "اختصار التنضيف (دوس واضغط)"),
        ["scope"] = ("What to fix", "إيه اللي يتصلّح"),
        ["scope0"] = ("Selection only", "المظلّل بس"),
        ["scope1"] = ("Selection, else line before cursor", "المظلّل، وإلا السطر اللي قبل المؤشر"),
        ["scope2"] = ("Selection, else whole text box", "المظلّل، وإلا الخانة كلها"),
        ["direction"] = ("Direction", "الاتجاه"),
        ["dir0"] = ("Auto-detect", "تلقائي"),
        ["dir1"] = ("Always English → Arabic", "دايمًا من إنجليزي لعربي"),
        ["dir2"] = ("Always Arabic → English", "دايمًا من عربي لإنجليزي"),
        ["enabled"] = ("Enabled", "شغّال"),
        ["startup"] = ("Start with Windows", "شغّله مع الويندوز"),
        ["switch"] = ("Switch keyboard layout after fixing", "بدّل لغة الكيبورد بعد التصليح"),
        ["restore"] = ("Restore clipboard after fixing", "رجّع اللي كان في الحافظة"),
        ["punct"] = ("Clean: fix punctuation (, ? ; → ، ؟ ؛)", "التنضيف: صلّح علامات الترقيم (, ? ; ← ، ؟ ؛)"),
        ["norm"] = ("Clean: unify letters (أ إ آ → ا, ى → ي), remove tashkeel", "التنضيف: وحّد الحروف (أ إ آ ← ا، ى ← ي) وشيل التشكيل"),
        ["lang"] = ("Language", "اللغة"),
        ["save"] = ("Save", "حفظ"),
        ["cancel"] = ("Cancel", "إلغاء"),
        ["taken"] = ("Shortcut {0} is taken. Pick another in Settings.", "الاختصار {0} واخده برنامج تاني. اختار غيره من الإعدادات."),
        ["uptodate"] = ("You are up to date.", "معاك آخر إصدار."),
        ["updating"] = ("Updating to {0}…", "بيتحدّث للإصدار {0}…"),
        ["updfail"] = ("Update check failed (offline?).", "فشل التحديث (مفيش نت؟)."),
        ["disabled"] = ("disabled", "موقوف"),
    };

    public static bool Arabic => Settings.Current.Language == "ar";
    public static string T(string key, params object[] args)
    {
        var (en, ar) = T_[key];
        return string.Format(Arabic ? ar : en, args);
    }
}
