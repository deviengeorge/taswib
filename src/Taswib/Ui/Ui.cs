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

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}

/// <summary>XAML text lookup: <c>Text="{l:Tr enabled}"</c>.</summary>
public sealed class Tr : Avalonia.Markup.Xaml.MarkupExtension
{
    public Tr(string key) => Key = key;
    public string Key { get; set; }
    public override object ProvideValue(IServiceProvider _) => T(Key);
}

static class Ui
{
    // Bundled (OFL), so Arabic looks the same on every PC; English keeps Segoe UI.
    public static readonly FontFamily ArabicFont = new($"avares://{typeof(Ui).Assembly.GetName().Name}/Assets/Fonts#Cairo");

    public static void Setup() => AppBuilder.Configure<App>().UseWin32().UseSkia().SetupWithoutStarting();

    /// <summary>Show a window and wait until it closes (the tray app has no owner window for ShowDialog).</summary>
    public static Task ShowAsync(Window w)
    {
        var done = new TaskCompletionSource();
        w.Closed += (_, _) => done.TrySetResult();
        if (Arabic) { w.FlowDirection = FlowDirection.RightToLeft; w.FontFamily = ArabicFont; w.Classes.Add("ar"); } // mirrors the whole layout
        w.Show(); w.Activate();
        return done.Task;
    }

    public static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}

/// <summary>Tray menu icons (WinForms menu): 16px bitmaps from the Windows icon font.</summary>
static class Icons
{
    public static System.Drawing.Bitmap Glyph(char c)
    {
        var bmp = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        using var f = new System.Drawing.Font("Segoe MDL2 Assets", 10f);
        System.Windows.Forms.TextRenderer.DrawText(g, c.ToString(), f, new System.Drawing.Rectangle(0, 0, 16, 16), System.Drawing.Color.FromArgb(70, 70, 70),
            System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPadding);
        return bmp;
    }
}
