using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using static Taswib.Strings;

namespace Taswib;

/// <summary>Avalonia application: loads the shared styles and light/dark colors from App.axaml, then starts the tray.</summary>
public partial class App : Application
{
    TrayContext? tray; // the whole app hangs off this; kept here so it lives as long as the application

    /// <summary>Loads App.axaml.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Logs UI-thread exceptions instead of crashing, then starts the tray.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) => { Log.Write("ui exception", e.Exception); e.Handled = true; };
        tray = new TrayContext(Program.Wake);
        Log.Write($"ready in {(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds:0} ms"); // process start to tray icon up
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>XAML text lookup: <c>Text="{l:Tr enabled}"</c>.</summary>
public sealed class Tr : Avalonia.Markup.Xaml.MarkupExtension
{
    /// <summary>Creates the lookup for one Strings key.</summary>
    public Tr(string key) => Key = key;
    /// <summary>Strings key to look up.</summary>
    public string Key { get; set; }
    /// <summary>Returns the text for the key in the current UI language.</summary>
    public override object ProvideValue(IServiceProvider _) => T(Key);
}

/// <summary>Avalonia helpers shared by all windows.</summary>
static class Ui
{
    /// <summary>Cairo, bundled (OFL), so Arabic looks the same on every PC; English keeps Segoe UI.</summary>
    public static readonly FontFamily ArabicFont = new($"avares://{typeof(Ui).Assembly.GetName().Name}/Assets/Fonts#Cairo");

    /// <summary>Arabic: mirrors the whole layout and switches to Cairo.</summary>
    public static void Localize(Window w)
    {
        if (Arabic) { w.FlowDirection = FlowDirection.RightToLeft; w.FontFamily = ArabicFont; w.Classes.Add("ar"); }
    }

    /// <summary>Show a window and wait until it closes (the tray app has no owner window for ShowDialog).</summary>
    public static Task ShowAsync(Window w)
    {
        var done = new TaskCompletionSource();
        w.Closed += (_, _) => done.TrySetResult();
        Localize(w);
        w.Show(); w.Activate();
        return done.Task;
    }

    /// <summary>Opens a URL or folder with its default app.</summary>
    public static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    /// <summary>Style that mirrors the tray menu (Avalonia draws it in its own popup) for Arabic.</summary>
    static readonly Style RtlMenu = new(x => x.Is<MenuFlyoutPresenter>())
    {
        Setters = { new Setter(Visual.FlowDirectionProperty, FlowDirection.RightToLeft), new Setter(Avalonia.Controls.Primitives.TemplatedControl.FontFamilyProperty, ArabicFont) },
    };

    /// <summary>Turns the Arabic tray-menu style on or off to match the UI language.</summary>
    public static void MirrorMenus()
    {
        var styles = Application.Current!.Styles;
        styles.Remove(RtlMenu);
        if (Arabic) styles.Add(RtlMenu);
    }

    /// <summary>Draws one Segoe MDL2 Assets glyph as a 16px tray-menu icon (rendered at 2x, so it stays sharp when scaled).</summary>
    public static Bitmap Glyph(char c)
    {
        var tb = new TextBlock
        {
            Text = c.ToString(), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)), // mid gray: readable on light and dark menus
            Width = 16, Height = 16, TextAlignment = TextAlignment.Center, Padding = new Thickness(0, 2, 0, 0),
        };
        tb.Measure(new Size(16, 16));
        tb.Arrange(new Rect(0, 0, 16, 16));
        var bmp = new RenderTargetBitmap(new PixelSize(32, 32), new Vector(192, 192));
        bmp.Render(tb);
        return bmp;
    }
}

/// <summary>Windows sounds (WinForms' SystemSounds is gone with WinForms).</summary>
static class Sound
{
    /// <summary>The "asterisk" sound, played after a successful fix.</summary>
    public static void Done() => Windows.Win32.PInvoke.MessageBeep(Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_STYLE.MB_ICONASTERISK);
    /// <summary>The default beep, played on a rejected key.</summary>
    public static void Beep() => Windows.Win32.PInvoke.MessageBeep(Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_STYLE.MB_OK);
}
