using FlashLoop.Core.Model;

namespace FlashLoop.Core.Stats;

public sealed record ForecastDay(DateOnly Date, int Count);

/// <summary>
/// 未來 N 天（含今天）的到期張數。第 0 天包含所有逾期卡；不含新卡；不套用每日上限，
/// 因此數字等於資料庫中 due_date 落在該日的卡片數。
/// </summary>
public static class ForecastCalculator
{
    public const int DefaultDays = 14;

    public static IReadOnlyList<ForecastDay> Forecast(IEnumerable<Card> cards, DateOnly today, int days = DefaultDays)
    {
        var counts = new int[days];
        foreach (var card in cards)
        {
            if (card.IsNew || card.Schedule.DueDate is not { } due)
            {
                continue;
            }
            var offset = Math.Max(0, due.DayNumber - today.DayNumber);
            if (offset < days)
            {
                counts[offset]++;
            }
        }
        return counts.Select((c, i) => new ForecastDay(today.AddDays(i), c)).ToList();
    }
}
