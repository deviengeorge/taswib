using Taswib.Core;
using static Taswib.Strings;

namespace Taswib;

sealed class SettingsForm : Form
{
    readonly ComboBox scope = Combo((T("scope0"), Scope.SelectionOnly), (T("scope1"), Scope.SelectionOrLine), (T("scope2"), Scope.SelectionOrAll));
    readonly ComboBox direction = Combo((T("dir0"), Direction.Auto), (T("dir1"), Direction.ToArabic), (T("dir2"), Direction.ToEnglish));
    readonly ComboBox language = Combo(("English", "en"), ("العربية (مصري)", "ar"));
    readonly CheckBox enabled = new() { Text = T("enabled"), AutoSize = true };
    readonly CheckBox startup = new() { Text = T("startup"), AutoSize = true };
    readonly CheckBox switchLayout = new() { Text = T("switch"), AutoSize = true };
    readonly CheckBox restore = new() { Text = T("restore"), AutoSize = true };
    readonly CheckBox punct = new() { Text = T("punct"), AutoSize = true };
    readonly CheckBox norm = new() { Text = T("norm"), AutoSize = true };
    string fixKey, cleanKey;

    public SettingsForm(Settings s, bool startupOn, Action<Settings, bool> apply)
    {
        Text = T("title");
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        if (Arabic) { RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; }

        fixKey = s.Hotkey; cleanKey = s.CleanHotkey;
        enabled.Checked = s.Enabled; startup.Checked = startupOn; restore.Checked = s.RestoreClipboard;
        switchLayout.Checked = s.SwitchLayout; punct.Checked = s.CleanPunctuation; norm.Checked = s.CleanNormalize;
        Select(scope, s.Scope); Select(direction, s.Direction); Select(language, s.Language);

        var fixBox = HotkeyBox(fixKey, k => fixKey = k);
        var cleanBox = HotkeyBox(cleanKey, k => cleanKey = k);

        var ok = new Button { Text = T("save"), DialogResult = DialogResult.OK, AutoSize = true };
        ok.Click += (_, _) =>
        {
            s.Enabled = enabled.Checked; s.Hotkey = fixKey; s.CleanHotkey = cleanKey;
            s.RestoreClipboard = restore.Checked; s.SwitchLayout = switchLayout.Checked;
            s.CleanPunctuation = punct.Checked; s.CleanNormalize = norm.Checked;
            s.Scope = (Scope)Value(scope); s.Direction = (Direction)Value(direction); s.Language = (string)Value(language);
            apply(s, startup.Checked);
        };
        var cancel = new Button { Text = T("cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = ok; CancelButton = cancel;

        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        void Row(string label, Control c) { t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 3) }); t.Controls.Add(c); }
        void Wide(Control c) { t.Controls.Add(c); t.SetColumnSpan(c, 2); }
        Row(T("hotkey"), fixBox);
        Row(T("cleankey"), cleanBox);
        Row(T("scope"), scope);
        Row(T("direction"), direction);
        Row(T("lang"), language);
        Wide(enabled); Wide(startup); Wide(switchLayout); Wide(restore); Wide(punct); Wide(norm);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right };
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
        Wide(buttons);
        Controls.Add(t);
    }

    static TextBox HotkeyBox(string initial, Action<string> set)
    {
        var b = new TextBox { ReadOnly = true, Width = 240, Text = initial, RightToLeft = RightToLeft.No };
        b.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true; e.Handled = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return; // wait for the real key
            var k = Settings.FormatHotkey(e.Modifiers, e.KeyCode);
            b.Text = k; set(k);
        };
        return b;
    }

    record Item(string Text, object Value) { public override string ToString() => Text; }

    static ComboBox Combo(params (string, object)[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        foreach (var (t, v) in items) c.Items.Add(new Item(t, v));
        return c;
    }

    static object Value(ComboBox c) => ((Item)c.SelectedItem!).Value;
    static void Select(ComboBox c, object v) => c.SelectedItem = c.Items.Cast<Item>().First(i => i.Value.Equals(v));
}
