using Taswib.Core;

// Smallest check that fails if the mapping breaks: dotnet run --project tests/Taswib.Check
void Eq(string got, string want) { if (got != want) { Console.Error.WriteLine($"FAIL: got '{got}' want '{want}'"); Environment.Exit(1); } }

Eq(Mapper.Convert("high fd; ;dt phg;"), "اهلا بيك كيف حالك");   // the original complaint
Eq(Mapper.Convert("اهلا بيك كيف حالك"), "high fd; ;dt phg;");   // reverse
Eq(Mapper.Convert("HIGH FD; ;DT PHG;", Direction.Auto, capsLock: true), "اهلا بيك كيف حالك"); // Caps Lock was on
Eq(Mapper.Convert("hg 123"), "ال 123");                          // digits stay
Eq(Mapper.Convert("اهلا", Direction.ToArabic), "اهلا");               // forced direction leaves unmapped text alone
Eq(Mapper.Convert("high", Direction.ToEnglish), "high");
Eq(Mapper.Resolve("high").ToString(), "ToArabic");
Eq(Mapper.Resolve("اهلا").ToString(), "ToEnglish");
Eq(TextTools.FixPunctuation("ماذا? نعم, ok; hi,"), "ماذا؟ نعم، ok; hi,");   // only after Arabic
Eq(TextTools.FixPunctuation("لا ; نعم"), "لا ؛ نعم");                     // space before is skipped
Eq(TextTools.Normalize("أحمد إبراهيم آمنة على"), "احمد ابراهيم امنة علي");
Eq(TextTools.Normalize("مُحَمَّد كــلام"), "محمد كلام");                  // tashkeel + tatweel removed
// The b key types ل+ا, same as g+h: each word must survive English -> Arabic -> English.
foreach (var w in new[] { "about", "table", "club", "possible", "high", "right", "though", "laughs", "roughly", "eight" })
    Eq(Mapper.Convert(Mapper.Convert(w)), w);
Eq(Mapper.Convert("لأ"), "G");                                        // Shift+G types two chars too
// Hotkey text: parse, reject typos, and round-trip what the recorder writes.
bool Hk(string s, out string r) { var ok = Hotkey.TryParse(s, out var m, out var k); r = ok ? $"{m}:{k}" : "fail"; return ok; }
Hk("Ctrl+Shift+Space", out var h); Eq(h, "6:Space");
Hk("Insert", out h); Eq(h, "0:Insert");                       // bare key allowed (the default)
Hk("Shift+Pause", out h); Eq(h, "4:Pause");
Hk("Crtl+K", out h); Eq(h, "fail");                           // typo must not register bare K
Hk("Ctrl+", out h); Eq(h, "fail");
Eq(Hotkey.Format(Hotkey.Alt | Hotkey.Ctrl | Hotkey.Shift, "F9"), "Ctrl+Shift+Alt+F9");
Hk(Hotkey.Format(Hotkey.Ctrl | Hotkey.Win, "D1"), out h); Eq(h, "10:D1");
// Key names: old saved names still work, Avalonia spellings map to them, letters can't be bare.
string Vk(string n) => Hotkey.TryKey(n, out var vk, out var c) ? $"{vk:X2}:{c}" : "fail";
Eq(Vk("Insert"), "2D:Insert");
Eq(Vk("Pause"), "13:Pause");
Eq(Vk("OemComma"), "BC:Oemcomma");                             // Avalonia name, saved WinForms spelling
Eq(Vk("Prior"), "21:PageUp");
Eq(Vk("D7"), "37:D7");
Eq(Vk("F24"), "87:F24");
Eq(Vk("LeftCtrl"), "fail");
Eq((Hotkey.BareOk(0x2D) && Hotkey.BareOk(0x7B) && !Hotkey.BareOk('A') && !Hotkey.BareOk(0x20)).ToString(), "True");
Console.WriteLine("ok");
