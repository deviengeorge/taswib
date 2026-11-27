using Avalonia.Controls;
using static Taswib.Strings;

namespace Taswib;

/// <summary>First-run window: explains the hotkey and gives a sample text to try it on.</summary>
public partial class WelcomeWindow : Window
{
    /// <summary>True if the user chose to open Settings when closing.</summary>
    public bool OpenSettings { get; private set; }
    /// <summary>Design-time constructor.</summary>
    public WelcomeWindow() : this("Insert") { }
    /// <summary>Shows the welcome text with the user's fix hotkey.</summary>
    public WelcomeWindow(string hotkey)
    {
        InitializeComponent();
        Body.Text = T("w_body", hotkey);
        OpenSettingsBtn.Click += (_, _) => { OpenSettings = true; Close(); };
        Ok.Click += (_, _) => Close();
        Opened += (_, _) => { Sample.Focus(); Sample.SelectAll(); }; // selected, so the shortcut works immediately
    }
}
