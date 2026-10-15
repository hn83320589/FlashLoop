using FlashLoop.Core.Model;

namespace FlashLoop.Core.Review;

/// <summary>
/// 某一天已完成的複習量，由當天的評分紀錄推導。
/// NewCardsIntroduced：當天第一次學習的新卡數（佔新卡額度）。
/// ReviewCardsDone：當天複習過的「非新卡」張數（佔複習額度；當天新學的卡重來後再評不重複佔用）。
/// CardsReviewed：當天評分過的不重複卡片數（統計畫面的「今日已複習張數」）。
/// </summary>
public sealed record DailyUsage(int NewCardsIntroduced, int ReviewCardsDone, int CardsReviewed, int Ratings)
{
    public static DailyUsage None { get; } = new(0, 0, 0, 0);

    public static DailyUsage FromLogs(IEnumerable<ReviewLog> logsOfDay)
    {
        var logs = logsOfDay.ToList();
        var newCardIds = logs.Where(l => l.WasNew).Select(l => l.CardId).ToHashSet();
        var reviewCardIds = logs.Where(l => !l.WasNew && !newCardIds.Contains(l.CardId)).Select(l => l.CardId).ToHashSet();
        var allCardIds = logs.Select(l => l.CardId).ToHashSet();
        return new DailyUsage(newCardIds.Count, reviewCardIds.Count, allCardIds.Count, logs.Count);
    }
}
