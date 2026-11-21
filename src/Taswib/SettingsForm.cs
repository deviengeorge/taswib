using Taswib.Core;

namespace Taswib;

sealed class SettingsForm : Form
{
    readonly TextBox hotkey = new() { ReadOnly = true, Width = 220 };
    readonly ComboBox scope = Combo(("Selection only", Scope.SelectionOnly), ("Selection, else line before cursor", Scope.SelectionOrLine), ("Selection, else whole text box", Scope.SelectionOrAll));
    readonly ComboBox direction = Combo(("Auto-detect", Direction.Auto), ("Always English → Arabic", Direction.ToArabic), ("Always Arabic → English", Direction.ToEnglish));
    readonly CheckBox enabled = new() { Text = "Enabled", AutoSize = true };
    readonly CheckBox startup = new() { Text = "Start with Windows", AutoSize = true };
    readonly CheckBox restore = new() { Text = "Restore clipboard after fixing", AutoSize = true };
    string pending;

    public SettingsForm(Settings s, bool startupOn, Action<Settings, bool> apply)
    {
        Text = "Taswib settings";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        pending = s.Hotkey; hotkey.Text = pending;
        enabled.Checked = s.Enabled; startup.Checked = startupOn; restore.Checked = s.RestoreClipboard;
        Select(scope, s.Scope); Select(direction, s.Direction);

        hotkey.KeyDown += (_, e) =>
        {
            e.SuppressKeyPress = true; e.Handled = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return; // wait for the real key
            pending = Settings.FormatHotkey(e.Modifiers, e.KeyCode);
            hotkey.Text = pending;
        };

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        ok.Click += (_, _) =>
        {
            s.Enabled = enabled.Checked; s.Hotkey = pending; s.RestoreClipboard = restore.Checked;
            s.Scope = (Scope)((Item)scope.SelectedItem!).Value; s.Direction = (Direction)((Item)direction.SelectedItem!).Value;
            apply(s, startup.Checked);
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = ok; CancelButton = cancel;

        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        void Row(string label, Control c) { t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 3) }); t.Controls.Add(c); }
        Row("Shortcut (click, press keys)", hotkey);
        Row("What to fix", scope);
        Row("Direction", direction);
        t.Controls.Add(enabled); t.SetColumnSpan(enabled, 2);
        t.Controls.Add(startup); t.SetColumnSpan(startup, 2);
        t.Controls.Add(restore); t.SetColumnSpan(restore, 2);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right };
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
        t.Controls.Add(buttons); t.SetColumnSpan(buttons, 2);
        Controls.Add(t);
    }

    record Item(string Text, object Value) { public override string ToString() => Text; }

    static ComboBox Combo(params (string, object)[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        foreach (var (t, v) in items) c.Items.Add(new Item(t, v));
        return c;
    }

    static void Select(ComboBox c, object v) => c.SelectedItem = c.Items.Cast<Item>().First(i => i.Value.Equals(v));
}
