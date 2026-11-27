namespace Taswib;

/// <summary>What's New popup content, one entry per feature release.</summary>
static class WhatsNew
{
    /// <summary>Add an entry ONLY for a release that brings a NEW FEATURE. Enhancements and bug fixes get no entry, so no popup.</summary>
    public static readonly (Version V, string En, string Ar)[] Entries =
    {
        (new Version(0, 7, 0),
         "• A sound and tray messages tell you when a fix worked, or why it didn't\n• Settings → About → Copy debug info, for bug reports\n• A welcome window on first launch",
         "• صوت ورسايل في الـ tray تقولك إن التصليح نجح، أو ليه مانجحش\n• الإعدادات ← عن البرنامج ← انسخ معلومات المشكلة، للإبلاغ عن أي مشكلة\n• نافذة ترحيب أول مرة تفتح البرنامج"),
    };

    /// <summary>The newest entry released after <paramref name="lastSeen"/> and up to <paramref name="current"/>.</summary>
    /// <returns>The entry to show, or null if there is nothing new.</returns>
    public static (Version V, string En, string Ar)? Pending(Version current, string lastSeen)
    {
        var seen = Version.TryParse(lastSeen, out var v) ? v : new Version(0, 0, 0);
        var hit = Entries.Where(e => e.V > seen && e.V <= current).OrderByDescending(e => e.V).ToArray();
        return hit.Length == 0 ? null : hit[0];
    }
}
