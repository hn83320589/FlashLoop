using FlashLoop.App.Controls;

namespace FlashLoop.App.Views;

/// <summary>統計：今日已複習張數、連續學習天數、未來 14 天到期預測。</summary>
public sealed class StatsView : UserControl
{
    private readonly AppServices _services;
    private readonly Label _today = MakeValueLabel();
    private readonly Label _streak = MakeValueLabel();
    private readonly Label _upcoming = MakeValueLabel();
    private readonly ForecastChart _chart = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };

    public StatsView(AppServices services)
    {
        _services = services;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(16);

        var tiles = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        for (var i = 0; i < 3; i++)
        {
            tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        }
        tiles.Controls.Add(MakeTile("今日已複習", _today), 0, 0);
        tiles.Controls.Add(MakeTile("連續學習天數", _streak), 1, 0);
        tiles.Controls.Add(MakeTile("未來 14 天到期", _upcoming), 2, 0);

        var chartTitle = new Label
        {
            Text = "未來 14 天到期張數（今天含逾期；不含新卡）",
            Font = UiTheme.Heading,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 16, 0, 8),
        };

        Controls.Add(_chart);
        Controls.Add(chartTitle);
        Controls.Add(tiles);
    }

    private static Label MakeValueLabel() => new() { AutoSize = true, Font = UiTheme.Big, ForeColor = UiTheme.Text };

    private static Control MakeTile(string caption, Label value)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = UiTheme.Surface,
            Padding = new Padding(16),
            Margin = new Padding(4),
        };
        panel.Controls.Add(new Label { Text = caption, AutoSize = true, ForeColor = UiTheme.MutedText });
        panel.Controls.Add(value);
        return panel;
    }

    public void RefreshData()
    {
        var s = _services.Stats.Get();
        _today.Text = $"{s.CardsReviewedToday} 張";
        if (s.RatingsToday != s.CardsReviewedToday)
        {
            _today.Text += $"（{s.RatingsToday} 次評分）";
        }
        _streak.Text = $"{s.Streak} 天";
        _upcoming.Text = $"{s.Forecast.Sum(d => d.Count)} 張";
        _chart.Days = s.Forecast;
    }
}
