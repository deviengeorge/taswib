using Taswib.Core;

// Smallest check that fails if the mapping breaks: dotnet run --project tests/Taswib.Check
void Eq(string got, string want) { if (got != want) { Console.Error.WriteLine($"FAIL: got '{got}' want '{want}'"); Environment.Exit(1); } }

Eq(Mapper.Convert("high fd; ;dt phg;"), "اهلا بيك كيف حالك");   // the original complaint
Eq(Mapper.Convert("اهلا بيك كيف حالك"), "high fd; ;dt phg;");   // reverse
Eq(Mapper.Convert("hg 123"), "ال 123");                          // digits stay
Eq(Mapper.Convert("اهلا", Direction.ToArabic), "اهلا");               // forced direction leaves unmapped text alone
Eq(Mapper.Convert("high", Direction.ToEnglish), "high");
Eq(Mapper.Resolve("high").ToString(), "ToArabic");
Eq(Mapper.Resolve("اهلا").ToString(), "ToEnglish");
Console.WriteLine("ok");
