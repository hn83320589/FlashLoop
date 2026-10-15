namespace FlashLoop.Core.Stats;

/// <summary>
/// 連續學習天數：有任一筆評分的日子算一天（同日多次只算一天）。
/// 今天已複習則從今天往回數；今天尚未複習則從昨天往回數（當天結束前不中斷）；
/// 昨天與今天都沒有複習即為 0。
/// </summary>
public static class StreakCalculator
{
    public static int Calculate(IEnumerable<DateOnly> reviewDates, DateOnly today)
    {
        var days = reviewDates.Where(d => d <= today).ToHashSet();
        var cursor = days.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (days.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }
}
