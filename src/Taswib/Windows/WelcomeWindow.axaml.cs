using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using static Taswib.Strings;
// WinForms is referenced too (tray, clipboard); these aliases pick the Avalonia type for the names both define.
using Window = Avalonia.Controls.Window;
using Button = Avalonia.Controls.Button;
using ListBox = Avalonia.Controls.ListBox;
using Border = Avalonia.Controls.Border;
using TextBlock = Avalonia.Controls.TextBlock;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using FlowDirection = Avalonia.Media.FlowDirection;
using Keys = System.Windows.Forms.Keys;
using Application = Avalonia.Application;
using FontFamily = Avalonia.Media.FontFamily;

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
