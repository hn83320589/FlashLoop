using FlashLoop.Core.Model;

namespace FlashLoop.Core.Review;

/// <summary>
/// 依每日上限建立當次複習佇列：先放到期卡（依到期日、Id），再放新卡（依 Id，即匯入順序）。
/// 超出上限的卡片狀態不變，之後的日子自然會再被選入，不會遺失。
/// </summary>
public static class ReviewQueueBuilder
{
    public static IReadOnlyList<Card> Build(IEnumerable<Card> cards, DailyUsage usageToday, DailyLimits limits, DateOnly today)
    {
        var list = cards as IReadOnlyCollection<Card> ?? cards.ToList();
        var reviewQuota = Math.Max(0, limits.ReviewsPerDay - usageToday.ReviewCardsDone);
        var newQuota = Math.Max(0, limits.NewCardsPerDay - usageToday.NewCardsIntroduced);

        var due = list.Where(c => c.IsDueOn(today))
            .OrderBy(c => c.Schedule.DueDate)
            .ThenBy(c => c.Id)
            .Take(reviewQuota);
        var fresh = list.Where(c => c.IsNew)
            .OrderBy(c => c.Id)
            .Take(newQuota);

        return due.Concat(fresh).ToList();
    }
}
