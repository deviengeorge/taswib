using Avalonia.Controls;
using static Taswib.Strings;

namespace Taswib;

/// <summary>What's New popup, shown once after an update that adds a feature.</summary>
public partial class NewsWindow : Window
{
    /// <summary>Design-time constructor.</summary>
    public NewsWindow() : this(new Version(0, 0), "") { }
    /// <summary>Shows the release notes; the title uses the version's major.minor.</summary>
    /// <param name="body">Notes in the current UI language.</param>
    public NewsWindow(Version v, string body)
    {
        InitializeComponent();
        Title = Head.Text = T("news_title", $"{v.Major}.{v.Minor}");
        Body.Text = body;
        Ok.Click += (_, _) => Close();
    }
}
