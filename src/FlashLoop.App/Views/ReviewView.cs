using System.Diagnostics;
using FlashLoop.Core.Model;
using FlashLoop.Core.Review;
using FlashLoop.Core.Scheduling;
using FlashLoop.Core.Time;

namespace FlashLoop.App.Views;

/// <summary>
/// 複習畫面。鍵盤：空白鍵翻面、1–4 評分、Ctrl+Z 撤銷、Esc 結束。
/// 完成後同一畫面切換為當日總結（總結畫面仍可 Ctrl+Z 撤銷並回到複習）。
/// </summary>
public sealed class ReviewView : UserControl
{
    private static readonly (Rating Rating, string Name, Color Color)[] RatingInfo =
    [
        (Rating.Again, "重來", UiTheme.Again),
        (Rating.Hard, "困難", UiTheme.Hard),
        (Rating.Good, "普通", UiTheme.Good),
        (Rating.Easy, "簡單", UiTheme.Easy),
    ];

    private readonly AppServices _services;
    private readonly Deck _deck;
    private readonly ReviewSession _session;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly bool _startedEmpty;

    private readonly Label _status = new() { AutoSize = true, ForeColor = UiTheme.MutedText, Dock = DockStyle.Fill };
    private readonly Label _front = MakeCardLabel(UiTheme.CardFront, UiTheme.Text);
    private readonly Label _back = MakeCardLabel(UiTheme.CardBack, UiTheme.Text);
    private readonly Label _tags = new() { AutoSize = true, ForeColor = UiTheme.MutedText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _hint = new() { AutoSize = true, ForeColor = UiTheme.MutedText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Button _flipButton;
    private readonly Button[] _rateButtons;
    private readonly Control _reviewPanel;
    private readonly Control _summaryPanel;
    private readonly Label _summaryTitle = new() { AutoSize = true, Font = UiTheme.Big, ForeColor = UiTheme.Text };
    private readonly Label _summaryBody = new() { AutoSize = true, Font = UiTheme.Heading, ForeColor = UiTheme.Text };
    private readonly Label _summaryHint = new() { AutoSize = true, ForeColor = UiTheme.MutedText };

    private bool _flipped;

    public event EventHandler? ExitRequested;
    public event EventHandler? StatsRequested;

    public ReviewView(AppServices services, Deck deck)
    {
        _services = services;
        _deck = deck;
        _session = services.Review.Start(deck.Id);
        _startedEmpty = _session.IsFinished;

        Dock = DockStyle.Fill;
        BackColor = UiTheme.Background;
        Padding = new Padding(16);

        _flipButton = MakeButton("翻面 (空白鍵)", UiTheme.Accent);
        _flipButton.Click += (_, _) => Flip();
        _rateButtons = RatingInfo.Select(info =>
        {
            var b = MakeButton("", info.Color);
            b.Click += (_, _) => Rate(info.Rating);
            return b;
        }).ToArray();

        _reviewPanel = BuildReviewPanel();
        _summaryPanel = BuildSummaryPanel();
        Controls.Add(_reviewPanel);
        Controls.Add(_summaryPanel);

        ShowCurrent();
    }

    /// <summary>是否仍有未評分的卡片（離開前需確認）。</summary>
    public bool InProgress => !_session.IsFinished;

    private Control BuildReviewPanel()
    {
        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
        };
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.Controls.Add(_front, 0, 0);
        card.Controls.Add(new Panel { BackColor = UiTheme.Border, Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 1);
        card.Controls.Add(_back, 0, 2);
        card.Controls.Add(_tags, 0, 3);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, RowCount = 1 };
        for (var i = 0; i < 5; i++)
        {
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        }
        buttons.Controls.Add(_flipButton, 0, 0);
        for (var i = 0; i < _rateButtons.Length; i++)
        {
            buttons.Controls.Add(_rateButtons[i], i + 1, 0);
        }

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_status, 0, 0);
        layout.Controls.Add(card, 0, 1);
        layout.Controls.Add(_hint, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        return layout;
    }

    private Control BuildSummaryPanel()
    {
        var back = MakeButton("回牌組列表 (Enter)", UiTheme.Accent);
        back.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        var stats = MakeButton("查看統計", UiTheme.MutedText);
        stats.Click += (_, _) => StatsRequested?.Invoke(this, EventArgs.Empty);
        back.Dock = stats.Dock = DockStyle.None;
        back.AutoSize = stats.AutoSize = true;

        var buttons = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 16, 0, 0) };
        buttons.Controls.AddRange([back, stats]);

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            BackColor = UiTheme.Surface,
            Padding = new Padding(32),
            Visible = false,
        };
        layout.Controls.AddRange([_summaryTitle, _summaryBody, buttons, _summaryHint]);
        _summaryBody.Margin = new Padding(3, 16, 3, 0);
        _summaryHint.Margin = new Padding(3, 16, 3, 0);
        return layout;
    }

    private static Label MakeCardLabel(Font font, Color color) => new()
    {
        Dock = DockStyle.Fill,
        Font = font,
        ForeColor = color,
        TextAlign = ContentAlignment.MiddleCenter,
        UseMnemonic = false,
    };

    private static Button MakeButton(string text, Color color)
    {
        var b = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Height = 56,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = UiTheme.Base,
            TabStop = false,
            UseMnemonic = false,
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Z))
        {
            Undo();
            return true;
        }

        if (_session.IsFinished)
        {
            if (keyData is Keys.Enter or Keys.Escape)
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        switch (keyData)
        {
            case Keys.Space:
                Flip();
                return true; // 不讓空白鍵觸發取得焦點的按鈕
            case Keys.D1 or Keys.NumPad1:
                Rate(Rating.Again);
                return true;
            case Keys.D2 or Keys.NumPad2:
                Rate(Rating.Hard);
                return true;
            case Keys.D3 or Keys.NumPad3:
                Rate(Rating.Good);
                return true;
            case Keys.D4 or Keys.NumPad4:
                Rate(Rating.Easy);
                return true;
            case Keys.Escape:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    private void Flip()
    {
        if (_session.IsFinished || _flipped)
        {
            return;
        }
        _flipped = true;
        ShowCurrent();
    }

    private void Rate(Rating rating)
    {
        if (_session.IsFinished || !_flipped)
        {
            return;
        }
        _session.Rate(rating);
        _flipped = false;
        ShowCurrent();
    }

    private void Undo()
    {
        if (_session.Undo())
        {
            _flipped = false;
            ShowCurrent();
        }
    }

    private void ShowCurrent()
    {
        if (_session.Current is not { } card)
        {
            ShowSummary();
            return;
        }

        _summaryPanel.Visible = false;
        _reviewPanel.Visible = true;

        var summary = _session.Summary;
        _status.Text = $"{_deck.Name}　·　剩餘 {_session.Remaining} 張　·　本次已評分 {summary.TotalRatings} 次" +
                       (card.IsNew ? "　·　新卡" : "");
        _front.Text = card.Front;
        _back.Text = _flipped ? card.Back : "";
        _tags.Text = _flipped && card.Tags.Count > 0 ? string.Join("　", card.Tags.Select(t => "#" + t)) : "";

        _flipButton.Enabled = !_flipped;
        _flipButton.BackColor = _flipped ? UiTheme.Border : UiTheme.Accent;
        var today = _services.Time.Today();
        for (var i = 0; i < _rateButtons.Length; i++)
        {
            var (rating, name, color) = RatingInfo[i];
            var interval = Sm2Scheduler.Apply(card.Schedule, rating, today).Interval;
            _rateButtons[i].Text = $"{(int)rating}  {name}\n{interval} 天後";
            _rateButtons[i].Enabled = _flipped;
            _rateButtons[i].BackColor = _flipped ? color : UiTheme.Border;
        }
        _hint.Text = _flipped
            ? "按 1 重來　2 困難　3 普通　4 簡單　·　Ctrl+Z 撤銷　·　Esc 結束"
            : "想好答案後按空白鍵翻面　·　Ctrl+Z 撤銷　·　Esc 結束";
    }

    private void ShowSummary()
    {
        _reviewPanel.Visible = false;
        _summaryPanel.Visible = true;
        var s = _session.Summary;
        var stats = _services.Stats.Get();

        if (_startedEmpty && s.TotalRatings == 0)
        {
            _summaryTitle.Text = "目前沒有需要複習的卡片";
            _summaryBody.Text = "今天的到期卡已複習完，或已達每日新卡／複習上限。\n" +
                                "超過上限的卡片會保留到之後的日子。";
        }
        else
        {
            _elapsed.Stop();
            _summaryTitle.Text = "本次複習完成！";
            _summaryBody.Text =
                $"複習卡片　{s.DistinctCards} 張（評分 {s.TotalRatings} 次）\n" +
                $"重來 {s.Again}　困難 {s.Hard}　普通 {s.Good}　簡單 {s.Easy}\n" +
                $"用時　{_elapsed.Elapsed:mm\\:ss}\n\n" +
                $"今日累計已複習　{stats.CardsReviewedToday} 張\n" +
                $"連續學習　{stats.Streak} 天\n" +
                $"明天到期　{stats.Forecast[1].Count} 張";
        }
        _summaryHint.Text = _session.CanUndo ? "按 Ctrl+Z 可撤銷最後一次評分並回到複習。" : "";
    }
}
