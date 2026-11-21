using static Taswib.Strings;

namespace Taswib;

sealed class NewsForm : Form
{
    public NewsForm(Version v, string body)
    {
        Text = T("news_title", $"{v.Major}.{v.Minor}");
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(540, 280);
        BackColor = Theme.Bg; Font = Theme.Body;
        var rtl = Arabic ? RightToLeft.Yes : RightToLeft.No;
        var head = new Label { Text = Text, Font = Theme.Title, ForeColor = Theme.Text, Location = new Point(24, 20), Size = new Size(492, 44), TextAlign = Arabic ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft, RightToLeft = rtl };
        var text = new Label { Text = body, ForeColor = Theme.Text, Location = new Point(24, 76), Size = new Size(492, 130), TextAlign = Arabic ? ContentAlignment.TopRight : ContentAlignment.TopLeft, RightToLeft = rtl };
        var ok = SettingsForm.Btn(T("w_ok"), Close);
        ok.Location = new Point(Arabic ? 24 : 540 - 24 - ok.Width, 222);
        Controls.AddRange(new Control[] { head, text, ok });
    }
}
