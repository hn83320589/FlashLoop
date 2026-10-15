using System.Drawing.Drawing2D;
using FlashLoop.Core.Stats;

namespace FlashLoop.App.Controls;

/// <summary>以 GDI+ 繪製的到期預測長條圖。每根長條上方標示張數，下方標示日期。</summary>
public sealed class ForecastChart : Control
{
    private IReadOnlyList<ForecastDay> _days = [];

    public ForecastChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = UiTheme.Surface;
        Font = UiTheme.Base;
        MinimumSize = new Size(400, 220);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<ForecastDay> Days
    {
        get => _days;
        set
        {
            _days = value ?? [];
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_days.Count == 0)
        {
            return;
        }

        var labelHeight = TextRenderer.MeasureText("00/00", Font).Height;
        var plot = new Rectangle(
            Padding.Left + 8,
            Padding.Top + labelHeight + 8,
            Width - Padding.Horizontal - 16,
            Height - Padding.Vertical - labelHeight * 3 - 16);
        if (plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        var max = Math.Max(1, _days.Max(d => d.Count));
        var slot = plot.Width / (float)_days.Count;
        var barWidth = Math.Max(4f, slot * 0.6f);

        using var axisPen = new Pen(UiTheme.Border);
        g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

        using var todayBrush = new SolidBrush(UiTheme.Hard);
        using var barBrush = new SolidBrush(UiTheme.Accent);

        for (var i = 0; i < _days.Count; i++)
        {
            var day = _days[i];
            var centerX = plot.Left + slot * i + slot / 2;
            var barHeight = plot.Height * day.Count / (float)max;
            var bar = new RectangleF(centerX - barWidth / 2, plot.Bottom - barHeight, barWidth, barHeight);
            if (barHeight > 0)
            {
                g.FillRectangle(i == 0 ? todayBrush : barBrush, bar);
            }

            DrawCentered(g, day.Count.ToString(), centerX, bar.Top - labelHeight - 2, UiTheme.Text);
            DrawCentered(g, i == 0 ? "今天" : day.Date.ToString("M/d"), centerX, plot.Bottom + 4, UiTheme.MutedText);
            DrawCentered(g, WeekdayName(day.Date.DayOfWeek), centerX, plot.Bottom + 4 + labelHeight, UiTheme.MutedText);
        }
    }

    private void DrawCentered(Graphics g, string text, float centerX, float top, Color color)
    {
        var size = TextRenderer.MeasureText(g, text, Font);
        TextRenderer.DrawText(g, text, Font, new Point((int)(centerX - size.Width / 2f), (int)top), color);
    }

    private static string WeekdayName(DayOfWeek d) => d switch
    {
        DayOfWeek.Sunday => "日",
        DayOfWeek.Monday => "一",
        DayOfWeek.Tuesday => "二",
        DayOfWeek.Wednesday => "三",
        DayOfWeek.Thursday => "四",
        DayOfWeek.Friday => "五",
        _ => "六",
    };
}
