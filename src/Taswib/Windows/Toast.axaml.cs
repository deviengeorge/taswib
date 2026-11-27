using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Taswib;

/// <summary>Small notification in the bottom corner of the screen; closes itself, or on click. One at a time.</summary>
public partial class Toast : Window
{
    static Toast? open;

    /// <summary>Builds the empty window (XAML previewer); use <see cref="Show(string, bool, int)"/>.</summary>
    public Toast() => InitializeComponent();

    /// <summary>Shows a message without taking focus, replacing any notification still on screen.</summary>
    /// <param name="warn">Draws the heading in the warning color.</param>
    /// <param name="ms">How long it stays up.</param>
    public static void Show(string msg, bool warn = false, int ms = 3000)
    {
        open?.Close();
        var t = open = new Toast();
        t.Body.Text = msg;
        t.Head.Classes.Set("warn", warn);
        Ui.Localize(t);
        var timer = DispatcherTimer.RunOnce(t.Close, TimeSpan.FromMilliseconds(ms));
        t.PointerPressed += (_, _) => t.Close();
        t.Closed += (_, _) => { timer.Dispose(); if (open == t) open = null; };
        t.Opened += (_, _) => t.PlaceInCorner();
        t.Show();
    }

    /// <summary>Moves the window above the taskbar's right end once its size is known, then fades it in (no jump).</summary>
    void PlaceInCorner()
    {
        if (Screens.Primary is not { } s) { Opacity = 1; return; }
        var size = PixelSize.FromSize(FrameSize ?? ClientSize, s.Scaling);
        var gap = (int)(12 * s.Scaling);
        Position = new PixelPoint(s.WorkingArea.Right - size.Width - gap, s.WorkingArea.Bottom - size.Height - gap);
        Opacity = 1;
    }
}
