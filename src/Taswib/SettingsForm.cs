using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Taswib.Core;
using static Taswib.Strings;

namespace Taswib;

sealed class SettingsForm : Form
{
    static int lastPage;

    readonly Settings s;
    readonly Action<Settings, bool> apply;
    readonly Action checkUpdate;
    bool startupOn;
    readonly Label title = new() { Dock = DockStyle.Top, Height = 48, Font = Theme.Title, ForeColor = Theme.Text, BackColor = Theme.Bg };
    readonly FlowLayoutPanel body = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Bg };
    readonly NavItem[] nav;

    /// <summary>Set when the language changed: the caller reopens the window so it is laid out for the new language.</summary>
    public bool Reopen { get; private set; }

    public SettingsForm(Settings s, bool startupOn, Action<Settings, bool> apply, Action checkUpdate)
    {
        this.s = s; this.startupOn = startupOn; this.apply = apply; this.checkUpdate = checkUpdate;
        Text = T("title");
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(790, 560);
        BackColor = Theme.Bg; Font = Theme.Body;

        nav = Names().Select(n => new NavItem(n)).ToArray();
        var side = new Panel { Dock = Arabic ? DockStyle.Right : DockStyle.Left, Width = 190, BackColor = Theme.Side };
        for (var i = nav.Length - 1; i >= 0; i--)
        {
            var idx = i;
            nav[i].Dock = DockStyle.Top;
            nav[i].Click += (_, _) => Show(idx);
            side.Controls.Add(nav[i]);
        }
        side.Controls.Add(new Brand { Dock = DockStyle.Top });
        // docked controls stack bottom-up in add order, so Brand (added last) ends on top

        title.TextAlign = Arabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft;
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 16), BackColor = Theme.Bg };
        content.Controls.Add(body);
        content.Controls.Add(title);
        Controls.Add(content);
        Controls.Add(side);
        Show(lastPage);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Theme.Dark) { var v = 1; DwmSetWindowAttribute(Handle, 20, ref v, sizeof(int)); } // dark title bar
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(nint h, int attr, ref int v, int size);

    static string[] Names() => new[] { T("nav_general"), T("nav_shortcuts"), T("nav_fix"), T("nav_clean"), T("nav_contact"), T("nav_about") };

    void Save() => apply(s, startupOn);

    void Show(int page)
    {
        lastPage = page;
        for (var i = 0; i < nav.Length; i++) { nav[i].Selected = i == page; nav[i].Invalidate(); }
        title.Text = Names()[page];
        foreach (Control c in body.Controls) c.Dispose();
        body.Controls.Clear();
        body.SuspendLayout();
        foreach (var c in page switch { 0 => General(), 1 => Shortcuts(), 2 => Fixing(), 3 => Cleaning(), 4 => ContactPage(), _ => About() }) body.Controls.Add(c);
        body.ResumeLayout();
    }

    static Toggle Tog(bool v, Action<bool> set)
    {
        var t = new Toggle(v);
        t.Changed += (_, _) => set(t.Checked);
        return t;
    }

    static Segmented Seg(string[] items, int selected, Action<int> set)
    {
        var g = new Segmented(items, selected);
        g.Changed += (_, _) => set(g.Selected);
        return g;
    }

    IEnumerable<Control> General()
    {
        yield return new Card(T("enabled"), T("enabled_d"), Tog(s.Enabled, v => { s.Enabled = v; Save(); }));
        yield return new Card(T("startup"), T("startup_d"), Tog(startupOn, v => { startupOn = v; Save(); }));
        yield return new Card(T("lang"), T("lang_d"), Seg(new[] { "English", "العربية" }, s.Language == "ar" ? 1 : 0, i =>
        {
            s.Language = i == 1 ? "ar" : "en"; Save(); Reopen = true; Close();
        }));
    }

    IEnumerable<Control> Shortcuts()
    {
        var f = new HotkeyBox(s.Hotkey, false); f.Changed += k => { s.Hotkey = k; Save(); };
        var c = new HotkeyBox(s.CleanHotkey, true); c.Changed += k => { s.CleanHotkey = k; Save(); };
        yield return new Card(T("t_fixkey"), T("fixkey_d"), f);
        yield return new Card(T("t_cleankey"), T("cleankey_d"), c);
    }

    IEnumerable<Control> Fixing()
    {
        yield return new Card(T("scope"), T("scope_d"), Seg(new[] { T("scopeS0"), T("scopeS1"), T("scopeS2") }, (int)s.Scope, i => { s.Scope = (Scope)i; Save(); }));
        yield return new Card(T("direction"), T("direction_d"), Seg(new[] { T("dirS0"), "EN→AR", "AR→EN" }, (int)s.Direction, i => { s.Direction = (Direction)i; Save(); }));
        yield return new Card(T("switch"), T("switch_d"), Tog(s.SwitchLayout, v => { s.SwitchLayout = v; Save(); }));
        yield return new Card(T("restore"), T("restore_d"), Tog(s.RestoreClipboard, v => { s.RestoreClipboard = v; Save(); }));
    }

    IEnumerable<Control> Cleaning()
    {
        yield return new Card(T("punct_t"), T("punct_d"), Tog(s.CleanPunctuation, v => { s.CleanPunctuation = v; Save(); }));
        yield return new Card(T("norm_t"), T("norm_d"), Tog(s.CleanNormalize, v => { s.CleanNormalize = v; Save(); }));
    }

    static readonly Bitmap Photo = new(typeof(SettingsForm).Assembly.GetManifestResourceStream("Taswib.contact.png")!);

    IEnumerable<Control> ContactPage()
    {
        yield return new Profile(Photo, Links.Name, T("c_sub"), T("c_intro"));
        yield return Link("c_email", Links.Email, "btn_send", "mailto:" + Links.Email);
        yield return Link("c_whatsapp", Links.PhoneDisplay, "btn_chat", Links.WhatsApp);
        yield return Link("c_linkedin", Links.LinkedInShort, "open", Links.LinkedIn);
        if (Links.TelegramUser != "") yield return Link("c_telegram", "@" + Links.TelegramUser, "btn_chat", "https://t.me/" + Links.TelegramUser);
    }

    static Card Link(string title, string value, string btn, string url) =>
        new(T(title), value, Btn(T(btn), () => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })));

    IEnumerable<Control> About()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version!;
        yield return new Card($"Taswib {ver.Major}.{ver.Minor}.{ver.Build}", T("about_d"), Btn(T("update"), () => checkUpdate()));
        yield return new Card(T("github"), "github.com/deviengeorge/taswib", Btn(T("open"), () =>
            Process.Start(new ProcessStartInfo("https://github.com/deviengeorge/taswib") { UseShellExecute = true })));
        yield return new Card(T("license"), "MIT", null);
    }

    static Button Btn(string text, Action click)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat, BackColor = Theme.Chip, ForeColor = Theme.Text, Font = Theme.Body,
            Height = 36, Width = TextRenderer.MeasureText(text, Theme.Body).Width + 36, Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => click();
        return b;
    }
}
