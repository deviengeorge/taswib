namespace Taswib;

/// <summary>Contact details shown on the Contact page. Telegram stays hidden until a username is set.</summary>
static class Links
{
    /// <summary>GitHub owner/name.</summary>
    public const string Repo = "deviengeorge/taswib"; // GitHub owner/name, used by the update check
    /// <summary>Author's full name.</summary>
    public const string Name = "Devien George Francis";
    /// <summary>Contact email.</summary>
    public const string Email = "deviengeorge99@gmail.com";
    /// <summary>Phone number as shown in the UI.</summary>
    public const string PhoneDisplay = "\u2066+20 128 108 9983\u2069"; // isolated left-to-right so Arabic layout does not reverse the digits
    /// <summary>WhatsApp chat link.</summary>
    public const string WhatsApp = "https://wa.me/201281089983";
    /// <summary>LinkedIn profile URL.</summary>
    public const string LinkedIn = "https://linkedin.com/in/deviengeorge";
    /// <summary>LinkedIn URL without the scheme, for display.</summary>
    public const string LinkedInShort = "linkedin.com/in/deviengeorge";
    /// <summary>Telegram username without @; empty hides the Telegram row.</summary>
    public static readonly string TelegramUser = "DevienGeorge";
}
