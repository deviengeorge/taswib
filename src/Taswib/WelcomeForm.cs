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
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(540, 330);
        BackColor = Theme.Bg; Font = Theme.Body;
        var align = Arabic ? ContentAlignment.TopRight : ContentAlignment.TopLeft;
        var rtl = Arabic ? RightToLeft.Yes : RightToLeft.No;

        var head = new Label { Text = T("w_title"), Font = Theme.Title, ForeColor = Theme.Text, Location = new Point(24, 20), Size = new Size(492, 44), TextAlign = Arabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft, RightToLeft = rtl };
        var body = new Label { Text = T("w_body", hotkey), ForeColor = Theme.Sub, Location = new Point(24, 72), Size = new Size(492, 52), TextAlign = align, RightToLeft = rtl };
        var box = new TextBox
        {
            Multiline = true, Text = "high fd; ;dt phg;", Font = new Font("Segoe UI", 16f), BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Card, ForeColor = Theme.Text, Location = new Point(24, 134), Size = new Size(492, 96),
        };
        var settings = SettingsForm.Btn(T("w_settings"), () => { OpenSettings = true; Close(); });
        var ok = SettingsForm.Btn(T("w_ok"), Close);
        settings.Location = new Point(Arabic ? 24 + ok.Width + 10 : 24, 252);
        ok.Location = new Point(Arabic ? 24 : 24 + settings.Width + 10, 252);
        Controls.AddRange(new Control[] { head, body, box, settings, ok });
        Shown += (_, _) => { box.Focus(); box.SelectAll(); }; // selected, so the shortcut works immediately
    }
}
