using FlashLoop.Core.Model;

namespace FlashLoop.App.Views;

/// <summary>全域每日上限設定（不分牌組）。</summary>
public sealed class SettingsView : UserControl
{
    private readonly AppServices _services;
    private readonly NumericUpDown _newLimit = MakeNumber();
    private readonly NumericUpDown _reviewLimit = MakeNumber();
    private readonly Label _message = new() { AutoSize = true, ForeColor = UiTheme.Good };

    public SettingsView(AppServices services)
    {
        _services = services;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(16);

        var save = new Button { Text = "儲存", AutoSize = true, Padding = new Padding(12, 4, 12, 4) };
        save.Click += (_, _) => Save();

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(16),
            BackColor = UiTheme.Surface,
            Location = new Point(Padding.Left, Padding.Top),
        };
        grid.Controls.Add(new Label { Text = "設定", Font = UiTheme.Heading, AutoSize = true, Margin = new Padding(3, 0, 3, 12) }, 0, 0);
        grid.SetColumnSpan(grid.GetControlFromPosition(0, 0)!, 2);
        AddRow(grid, 1, "每日新卡上限", _newLimit);
        AddRow(grid, 2, "每日複習上限", _reviewLimit);
        grid.Controls.Add(new Label
        {
            Text = "上限為全域設定，所有牌組共用。超出上限的卡片會順延到之後的日子，不會遺失。",
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            ForeColor = UiTheme.MutedText,
            Margin = new Padding(3, 12, 3, 12),
        }, 0, 3);
        grid.SetColumnSpan(grid.GetControlFromPosition(0, 3)!, 2);
        grid.Controls.Add(save, 0, 4);
        grid.Controls.Add(_message, 1, 4);

        Controls.Add(grid);
    }

    private static NumericUpDown MakeNumber() => new() { Minimum = 0, Maximum = 9999, Width = 100 };

    private static void AddRow(TableLayoutPanel grid, int row, string caption, Control input)
    {
        grid.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 24, 6) }, 0, row);
        grid.Controls.Add(input, 1, row);
    }

    public void RefreshData()
    {
        var limits = _services.Settings.LoadLimits();
        _newLimit.Value = limits.NewCardsPerDay;
        _reviewLimit.Value = limits.ReviewsPerDay;
        _message.Text = "";
        _newLimit.Focus();
    }

    private void Save()
    {
        _services.Settings.SaveLimits(new DailyLimits((int)_newLimit.Value, (int)_reviewLimit.Value));
        _message.Text = "已儲存";
    }
}
