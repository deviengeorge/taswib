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
