namespace FlashLoop.App.Dialogs;

/// <summary>單行文字輸入對話框（新增、重新命名牌組）。Enter 確定、Esc 取消。</summary>
public sealed class TextPromptDialog : Form
{
    private readonly TextBox _input;

    private TextPromptDialog(string title, string prompt, string initial)
    {
        Text = title;
        Font = UiTheme.Base;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _input = new TextBox { Text = initial, Width = 320, Dock = DockStyle.Fill };
        var ok = new Button { Text = "確定", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.AddRange([cancel, ok]);

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label { Text = prompt, AutoSize = true, Margin = new Padding(0, 0, 0, 6) });
        layout.Controls.Add(_input);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => _input.SelectAll();
    }

    /// <summary>回傳輸入文字；取消時回傳 null。</summary>
    public static string? Ask(IWin32Window owner, string title, string prompt, string initial = "")
    {
        using var dialog = new TextPromptDialog(title, prompt, initial);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._input.Text : null;
    }
}
