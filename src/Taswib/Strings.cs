namespace Taswib;

// UI text. Arabic is Egyptian, not MSA.
static class Strings
{
    static readonly Dictionary<string, (string En, string Ar)> T_ = new()
    {
        ["title"] = ("Taswib settings", "إعدادات تصويب"),
        ["nav_general"] = ("General", "عام"),
        ["nav_shortcuts"] = ("Shortcuts", "الاختصارات"),
        ["nav_fix"] = ("Fixing", "التصليح"),
        ["nav_clean"] = ("Cleaning", "التنضيف"),
        ["nav_about"] = ("About", "عن البرنامج"),
        ["enabled_d"] = ("Turn Taswib on or off", "شغّل تصويب أو وقّفه"),
        ["startup_d"] = ("Launch Taswib when you sign in", "يشتغل أول ما تفتح الويندوز"),
        ["lang_d"] = ("Interface language", "لغة الواجهة"),
        ["t_fixkey"] = ("Fix shortcut", "اختصار التصليح"),
        ["t_cleankey"] = ("Clean shortcut", "اختصار التنضيف"),
        ["fixkey_d"] = ("Click the box, then press the keys", "دوس على الخانة وبعدين اضغط الأزرار"),
        ["cleankey_d"] = ("Delete clears it", "زرار Delete بيمسحه"),
        ["scope_d"] = ("Nothing selected? Also fix…", "لو مفيش حاجة مظلّلة، صلّح كمان…"),
        ["scopeS0"] = ("Selection", "المظلّل"),
        ["scopeS1"] = ("+ Line", "+ السطر"),
        ["scopeS2"] = ("+ Whole box", "+ الخانة كلها"),
        ["direction_d"] = ("Which way to convert", "يحوّل في أنهي اتجاه"),
        ["dirS0"] = ("Auto", "تلقائي"),
        ["switch_d"] = ("Jump to the right language after fixing", "يروح للغة الصح بعد التصليح"),
        ["restore_d"] = ("Put back what you had copied", "يرجّع اللي كنت ناسخه"),
        ["punct_t"] = ("Fix punctuation", "صلّح علامات الترقيم"),
        ["punct_d"] = ("Example:  , ? ;   →   ، ؟ ؛", "مثال:  , ? ;   ←   ، ؟ ؛"),
        ["norm_t"] = ("Unify letters", "وحّد الحروف"),
        ["norm_d"] = ("أ إ آ → ا  ·  ى → ي  ·  removes tashkeel", "أ إ آ ← ا  ·  ى ← ي  ·  وبيشيل التشكيل"),
        ["about_d"] = ("Fix text typed in the wrong keyboard layout", "صلّح الكلام اللي اتكتب بلغة الكيبورد الغلط"),
        ["github"] = ("GitHub", "GitHub"),
        ["open"] = ("Open", "افتح"),
        ["license"] = ("License", "الرخصة"),
        ["fixnow"] = ("Fix text now", "صلّح الكلام دلوقتي"),
        ["cleannow"] = ("Clean text now", "نضّف الكلام دلوقتي"),
        ["settings"] = ("Settings…", "الإعدادات…"),
        ["update"] = ("Check for updates", "دوّر على تحديث"),
        ["exit"] = ("Exit", "خروج"),
        ["scope"] = ("What to fix", "إيه اللي يتصلّح"),
        ["direction"] = ("Direction", "الاتجاه"),
        ["enabled"] = ("Enabled", "شغّال"),
        ["startup"] = ("Start with Windows", "شغّله مع الويندوز"),
        ["switch"] = ("Switch keyboard layout after fixing", "بدّل لغة الكيبورد بعد التصليح"),
        ["restore"] = ("Restore clipboard after fixing", "رجّع اللي كان في الحافظة"),
        ["lang"] = ("Language", "اللغة"),
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
