using FlashLoop.App.Controls;
using FlashLoop.App.Tests.Support;
using FlashLoop.Core.Model;

namespace FlashLoop.App.Tests;

/// <summary>
/// 驗收 #7（每日上限與順延）、#9（streak）、#10（預測圖與資料庫一致）的多日情境：
/// 第 1 天複習、第 2 天調低上限、跳過第 3 天、第 4 天回來。每天都重新啟動程式。
/// </summary>
public class MultiDayFlowTests
{
    private const string N2 = "N2 基礎";
    private const string Talk = "會話";

    [Fact]
    public void Limits_streak_and_forecast_stay_consistent_across_days() => Sta.Run(app =>
    {
        var samples = Path.Combine(AppContext.BaseDirectory, "samples");
        app.Services.Import.Import(app.Services.Decks.Create(N2).Id, File.ReadAllBytes(Path.Combine(samples, "n2_basic.csv")));
        app.Services.Import.Import(app.Services.Decks.Create(Talk).Id, File.ReadAllBytes(Path.Combine(samples, "special_bom.csv")));

        // 第 1 天：預設上限新卡 20，N2 用完全域額度後會話牌組沒有新卡
        Assert.Equal(20, app.ReviewAll(N2, Keys.D3).DistinctCards);
        Assert.Equal(0, app.ReviewAll(Talk, Keys.D3).DistinctCards);
        AssertStatsScreen(app, reviewedToday: 20, streak: 1);

        // 第 2 天：上限改為新 5／複習 15
        app.Time.AdvanceDays(1);
        app.Settings.SaveLimits(new DailyLimits(5, 15));
        app.Restart();
        Assert.Equal($"{N2} 30/15/5", app.DeckRows()[0]);
        Assert.Equal(20, DueInN2(app));
        Assert.Equal(10, app.Scalar(N2Cards("c.due_date IS NULL")));
        Assert.Equal(1, app.Services.Stats.Get().Streak);
        Assert.Equal(20, app.ReviewAll(N2, Keys.D3).DistinctCards);
        Assert.Equal(5, DueInN2(app)); // 超過複習上限的到期卡保留為逾期
        Assert.Equal(2, app.Services.Stats.Get().Streak);

        // 跳過第 3 天，第 4 天
        app.Time.AdvanceDays(2);
        app.Restart();
        Assert.Equal(0, app.Services.Stats.Get().Streak);
        Assert.Equal($"{N2} 30/10/5", app.DeckRows()[0]); // 逾期的 5 張加上今天到期的 5 張
        Assert.True(app.ReviewAll(Talk, Keys.D3).DistinctCards > 0);
        Assert.True(app.ReviewAll(N2, Keys.D3).DistinctCards > 0);
        AssertStatsScreen(app, reviewedToday: 15, streak: 1); // 同一天兩次複習只算一天
    });

    private static long DueInN2(AppHarness app) => app.Scalar(N2Cards($"c.due_date <= '{app.Today}'"));

    private static string N2Cards(string condition) =>
        $"SELECT COUNT(*) FROM cards c JOIN decks d ON d.id = c.deck_id WHERE d.name = '{N2}' AND {condition}";

    private static void AssertStatsScreen(AppHarness app, int reviewedToday, int streak)
    {
        app.Press(app.Form, Keys.Control | Keys.D2);
        var stats = app.StatsView;
        Assert.StartsWith($"{reviewedToday} 張", AppHarness.Field<Label>(stats, "_today").Text);
        Assert.Equal($"{streak} 天", AppHarness.Field<Label>(stats, "_streak").Text);

        var chart = AppHarness.Field<ForecastChart>(stats, "_chart");
        var today = DateOnly.Parse(app.Today);
        Assert.Equal(today, chart.Days[0].Date);
        var expected = Enumerable.Range(0, 14).Select(i =>
        {
            var day = today.AddDays(i).ToString("yyyy-MM-dd");
            return (int)app.Scalar(i == 0
                ? $"SELECT COUNT(*) FROM cards WHERE due_date IS NOT NULL AND due_date <= '{day}'"
                : $"SELECT COUNT(*) FROM cards WHERE due_date = '{day}'");
        });
        Assert.Equal(expected, chart.Days.Select(d => d.Count));
        app.Press(app.Form, Keys.Control | Keys.D1);
    }
}
