using static Taswib.Strings;

namespace Taswib;

sealed class WelcomeForm : Form
{
    public bool OpenSettings { get; private set; }

    public WelcomeForm(string hotkey)
    {
        Text = T("w_title");
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; TopMost = true;
        AutoScaleMode = AutoScaleMode.None; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(Theme.S(540), Theme.S(330));
        BackColor = Theme.Bg; Font = Theme.Body;
        var align = Arabic ? ContentAlignment.TopRight : ContentAlignment.TopLeft;
        var rtl = Arabic ? RightToLeft.Yes : RightToLeft.No;

        var head = new Label { Text = T("w_title"), Font = Theme.Title, ForeColor = Theme.Text, Location = new Point(Theme.S(24), Theme.S(20)), Size = new Size(Theme.S(492), Theme.S(44)), TextAlign = Arabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft, RightToLeft = rtl };
        var body = new Label { Text = T("w_body", hotkey), ForeColor = Theme.Sub, Location = new Point(Theme.S(24), Theme.S(72)), Size = new Size(Theme.S(492), Theme.S(52)), TextAlign = align, RightToLeft = rtl };
        var box = new TextBox
        {
            Multiline = true, Text = "high fd; ;dt phg;", Font = new Font("Segoe UI", 16f), BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Card, ForeColor = Theme.Text, Location = new Point(Theme.S(24), Theme.S(134)), Size = new Size(Theme.S(492), Theme.S(96)),
        };
        var settings = SettingsForm.Btn(T("w_settings"), () => { OpenSettings = true; Close(); });
        var ok = SettingsForm.Btn(T("w_ok"), Close);
        int m = Theme.S(24), gap = Theme.S(10), y = Theme.S(252);
        settings.Location = new Point(Arabic ? m + ok.Width + gap : m, y);
        ok.Location = new Point(Arabic ? m : m + settings.Width + gap, y);
        Controls.AddRange(new Control[] { head, body, box, settings, ok });
        Shown += (_, _) => { box.Focus(); box.SelectAll(); }; // selected, so the shortcut works immediately
    }
}
