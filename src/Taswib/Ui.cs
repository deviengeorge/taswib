using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace Taswib;

static class Theme
{
    public static readonly bool Dark = IsDark();
    public static readonly Color Bg = Dark ? C(32, 32, 32) : C(243, 243, 245);
    public static readonly Color Side = Dark ? C(24, 24, 24) : C(234, 234, 238);
    public static readonly Color Card = Dark ? C(44, 44, 47) : Color.White;
    public static readonly Color Border = Dark ? C(66, 66, 70) : C(226, 226, 231);
    public static readonly Color Chip = Dark ? C(62, 62, 67) : C(238, 238, 243);
    public static readonly Color Text = Dark ? C(242, 242, 244) : C(28, 28, 30);
    public static readonly Color Sub = Dark ? C(165, 165, 172) : C(105, 105, 114);
    public static readonly Color Accent = C(79, 70, 229);
    public static readonly Font Body = new("Segoe UI", 10f);
    public static readonly Font Small = new("Segoe UI", 9f);
    public static readonly Font Semi = new("Segoe UI Semibold", 10f);
    public static readonly Font Title = new("Segoe UI Semibold", 18f);
    public static readonly Font Name = new("Segoe UI Semibold", 14f);

    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    static bool IsDark()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int v && v == 0; }
        catch { return false; }
    }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        var d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // Arabic is laid out by hand (not WinForms mirroring), so text just needs right alignment + RTL reading order.
    public static TextFormatFlags Flags(bool center = false)
    {
        var f = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        if (center) return f | TextFormatFlags.HorizontalCenter;
        return Strings.Arabic ? f | TextFormatFlags.Right | TextFormatFlags.RightToLeft : f;
    }
}

abstract class Base : Control
{
    protected Base() { DoubleBuffered = true; ResizeRedraw = true; }
}

sealed class Toggle : Base
{
    bool on;
    public event EventHandler? Changed;
    public bool Checked
    {
        get => on;
        set { if (on == value) return; on = value; Invalidate(); Changed?.Invoke(this, EventArgs.Empty); }
    }
    public Toggle(bool initial) { on = initial; Size = new Size(46, 26); Cursor = Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Card); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new RectangleF(1, 1, Width - 2, Height - 2), (Height - 2) / 2f);
        using var fill = new SolidBrush(on ? Theme.Accent : (Theme.Dark ? Color.FromArgb(90, 90, 96) : Color.FromArgb(190, 190, 198)));
        g.FillPath(fill, path);
        var d = Height - 10;
        g.FillEllipse(Brushes.White, on ? Width - d - 5 : 5, 5, d, d);
    }
    protected override void OnClick(EventArgs e) { Checked = !on; base.OnClick(e); }
}

sealed class Segmented : Base
{
    readonly string[] items;
    readonly int[] w;
    public int Selected { get; private set; }
    public event EventHandler? Changed;

    public Segmented(string[] items, int selected)
    {
        this.items = items; Selected = selected; Cursor = Cursors.Hand;
        w = items.Select(i => TextRenderer.MeasureText(i, Theme.Body).Width + 30).ToArray();
        Size = new Size(w.Sum() + 8, 36);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Card); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 9)) using (var b = new SolidBrush(Theme.Chip)) g.FillPath(b, bg);
        var x = 4;
        for (var i = 0; i < items.Length; i++)
        {
            var r = new Rectangle(x, 4, w[i], Height - 8);
            if (i == Selected) { using var p = Theme.Round(r, 7); using var b = new SolidBrush(Theme.Accent); g.FillPath(b, p); }
            TextRenderer.DrawText(g, items[i], Theme.Body, r, i == Selected ? Color.White : Theme.Text, Theme.Flags(center: true));
            x += w[i];
        }
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        var x = 4;
        for (var i = 0; i < items.Length; i++)
        {
            if (e.X >= x && e.X < x + w[i]) { if (i != Selected) { Selected = i; Invalidate(); Changed?.Invoke(this, EventArgs.Empty); } return; }
            x += w[i];
        }
    }
}

sealed class HotkeyBox : Base
{
    readonly bool allowClear;
    bool captured;
    public string Value { get; private set; }
    public event Action<string>? Changed;

    public HotkeyBox(string value, bool allowClear)
    {
        Value = value; this.allowClear = allowClear;
        SetStyle(ControlStyles.Selectable, true); TabStop = true; Cursor = Cursors.Hand; Size = new Size(190, 36);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Card); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 9);
        using (var b = new SolidBrush(Theme.Chip)) g.FillPath(b, path);
        using (var pen = new Pen(Focused ? Theme.Accent : Theme.Border, Focused ? 2f : 1f)) g.DrawPath(pen, path);
        var text = Focused && !captured ? "…" : (Value == "" ? "—" : Value); // "…" only until a key is pressed
        TextRenderer.DrawText(g, text, Theme.Semi, ClientRectangle, Theme.Text, Theme.Flags(center: true));
    }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
    protected override void OnGotFocus(EventArgs e) { captured = false; Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys keyData) => true;
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!Focused) return base.ProcessCmdKey(ref msg, keyData);
        var key = keyData & Keys.KeyCode; var mods = keyData & Keys.Modifiers;
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return true; // wait for the real key
        if (key == Keys.Delete && mods == 0 && allowClear) Set("");
        else if (key != Keys.Escape && key != Keys.Tab)
        {
            if (mods == 0 && !BareOk(key)) System.Media.SystemSounds.Beep.Play(); // a bare letter would be hijacked system-wide; keep old value
            else Set(Settings.FormatHotkey(mods, key));
        }
        return key != Keys.Tab; // Tab still moves focus away
    }
    // Keys that are safe to own globally with no modifier.
    static bool BareOk(Keys k) => (k >= Keys.F1 && k <= Keys.F24)
        || k is Keys.Insert or Keys.Pause or Keys.Scroll or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.Delete;
    public void Set(string v) { Value = v; captured = true; Invalidate(); Changed?.Invoke(v); }
}

sealed class Card : Panel
{
    readonly string title, desc;
    readonly Control? right;

    public Card(string title, string desc, Control? right)
    {
        this.title = title; this.desc = desc; this.right = right;
        DoubleBuffered = true; Size = new Size(534, 66); Margin = new Padding(0, 0, 0, 10);
        if (right != null) Controls.Add(right);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (right != null) right.Location = new Point(Strings.Arabic ? 18 : Width - 18 - right.Width, (Height - right.Height) / 2);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Bg); g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 10))
        {
            using var b = new SolidBrush(Theme.Card); g.FillPath(b, p);
            using var pen = new Pen(Theme.Border); g.DrawPath(pen, p);
        }
        var rw = right == null ? 0 : right.Width + 16;
        var x = Strings.Arabic ? 18 + rw : 18;
        var w = Width - 36 - rw;
        var t = desc == "" ? new Rectangle(x, 0, w, Height) : new Rectangle(x, 11, w, 24);
        TextRenderer.DrawText(g, title, Theme.Semi, t, Theme.Text, Theme.Flags());
        if (desc != "") TextRenderer.DrawText(g, desc, Theme.Small, new Rectangle(x, 35, w, 22), Theme.Sub, Theme.Flags());
    }
}

sealed class NavItem : Base
{
    readonly string text;
    bool hover;
    public bool Selected { get; set; }
    public NavItem(string text) { this.text = text; Size = new Size(190, 44); Cursor = Cursors.Hand; }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Side); g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Selected || hover)
        {
            using var p = Theme.Round(new RectangleF(10, 4, Width - 20, Height - 8), 8);
            using var b = new SolidBrush(Selected ? Theme.Chip : Color.FromArgb(40, Theme.Chip));
            g.FillPath(b, p);
        }
        if (Selected)
        {
            var bx = Strings.Arabic ? Width - 14 : 10;
            using var b = new SolidBrush(Theme.Accent);
            g.FillRectangle(b, bx, 14, 3, Height - 28);
        }
        TextRenderer.DrawText(g, text, Selected ? Theme.Semi : Theme.Body, new Rectangle(28, 0, Width - 56, Height), Theme.Text, Theme.Flags());
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
}

sealed class Brand : Base
{
    readonly Bitmap icon;
    public Brand() { Size = new Size(190, 64); icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!.ToBitmap(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Side); g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        var ix = Strings.Arabic ? Width - 18 - 30 : 18;
        g.DrawImage(icon, new Rectangle(ix, 17, 30, 30));
        var tr = Strings.Arabic ? new Rectangle(10, 0, Width - 18 - 30 - 20, Height) : new Rectangle(58, 0, Width - 70, Height);
        TextRenderer.DrawText(g, "Taswib", Theme.Semi, tr, Theme.Text, Theme.Flags());
    }
}

sealed class Profile : Base
{
    readonly Bitmap photo;
    readonly string name, sub, intro;

    public Profile(Bitmap photo, string name, string sub, string intro)
    {
        this.photo = photo; this.name = name; this.sub = sub; this.intro = intro;
        Size = new Size(534, 132); Margin = new Padding(0, 0, 0, 10);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Bg); g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using (var p = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 10))
        {
            using var b = new SolidBrush(Theme.Card); g.FillPath(b, p);
            using var pen = new Pen(Theme.Border); g.DrawPath(pen, p);
        }
        const int d = 92;
        var px = Strings.Arabic ? Width - 18 - d : 18;
        var circle = new Rectangle(px, (Height - d) / 2, d, d);
        using (var cp = new GraphicsPath()) { cp.AddEllipse(circle); g.SetClip(cp); g.DrawImage(photo, circle); g.ResetClip(); }
        using (var ring = new Pen(Theme.Accent, 2f)) g.DrawEllipse(ring, circle);

        var w = Width - 18 - d - 18 - 18;
        var x = Strings.Arabic ? 18 : 18 + d + 18;
        TextRenderer.DrawText(g, name, Theme.Name, new Rectangle(x, 20, w, 28), Theme.Text, Theme.Flags());
        TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(x, 50, w, 22), Theme.Sub, Theme.Flags());
        var wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | (Strings.Arabic ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : 0);
        TextRenderer.DrawText(g, intro, Theme.Body, new Rectangle(x, 80, w, 40), Theme.Text, wrap);
    }
}
