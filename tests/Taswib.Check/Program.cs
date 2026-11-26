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
Console.WriteLine("ok");
