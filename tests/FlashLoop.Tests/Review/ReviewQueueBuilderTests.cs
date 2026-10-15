using FlashLoop.Core.Model;
using FlashLoop.Core.Review;
using FlashLoop.Core.Scheduling;
using FlashLoop.Core.Time;
using FlashLoop.Tests.Support;

namespace FlashLoop.Tests.Review;

public class ReviewQueueBuilderTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Fact]
    public void Includes_overdue_and_due_today_but_not_future_cards()
    {
        Card[] cards =
        [
            TestCards.Due(1, Today.AddDays(-3)),
            TestCards.Due(2, Today),
            TestCards.Due(3, Today.AddDays(1)),
        ];
        var queue = ReviewQueueBuilder.Build(cards, DailyUsage.None, DailyLimits.Default, Today);
        Assert.Equal([1L, 2L], queue.Select(c => c.Id));
    }

    [Fact]
    public void Due_cards_come_first_ordered_by_due_date_then_new_cards_in_creation_order()
    {
        Card[] cards =
        [
            TestCards.New(10),
            TestCards.Due(2, Today),
            TestCards.New(5),
            TestCards.Due(3, Today.AddDays(-1)),
        ];
        var queue = ReviewQueueBuilder.Build(cards, DailyUsage.None, DailyLimits.Default, Today);
        Assert.Equal([3L, 2L, 5L, 10L], queue.Select(c => c.Id));
    }

    [Fact]
    public void New_card_limit_applies()
    {
        var cards = Enumerable.Range(1, 30).Select(i => TestCards.New(i)).ToList();
        var queue = ReviewQueueBuilder.Build(cards, DailyUsage.None, new DailyLimits(20, 100), Today);
        Assert.Equal(20, queue.Count);
        Assert.All(queue, c => Assert.True(c.IsNew));
    }

    [Fact]
    public void Review_limit_applies()
    {
        var cards = Enumerable.Range(1, 150).Select(i => TestCards.Due(i, Today)).ToList();
        var queue = ReviewQueueBuilder.Build(cards, DailyUsage.None, new DailyLimits(20, 100), Today);
        Assert.Equal(100, queue.Count);
    }

    [Fact]
    public void Limits_subtract_what_was_already_done_today()
    {
        var cards = Enumerable.Range(1, 30).Select(i => TestCards.New(i))
            .Concat(Enumerable.Range(100, 30).Select(i => TestCards.Due(i, Today)))
            .ToList();
        var usage = new DailyUsage(NewCardsIntroduced: 15, ReviewCardsDone: 95, CardsReviewed: 110, Ratings: 120);

        var queue = ReviewQueueBuilder.Build(cards, usage, new DailyLimits(20, 100), Today);

        Assert.Equal(5, queue.Count(c => c.IsNew));
        Assert.Equal(5, queue.Count(c => !c.IsNew));
    }

    [Fact]
    public void Limits_never_go_negative()
    {
        var cards = new[] { TestCards.New(1), TestCards.Due(2, Today) };
        var usage = new DailyUsage(50, 500, 550, 550);
        Assert.Empty(ReviewQueueBuilder.Build(cards, usage, new DailyLimits(20, 100), Today));
    }

    [Fact]
    public void Daily_usage_counts_distinct_cards_and_does_not_count_new_card_relearn_as_review()
    {
        var day = Today;
        ReviewLog Log(long id, long card, Rating r, bool wasNew) =>
            new(id, Guid.NewGuid(), card, DateTimeOffset.UnixEpoch, day, r, wasNew);

        ReviewLog[] logs =
        [
            Log(1, 1, Rating.Again, wasNew: true),   // 新卡第一次
            Log(2, 1, Rating.Good, wasNew: false),   // 同一張卡重來後再評
            Log(3, 2, Rating.Again, wasNew: false),  // 複習卡
            Log(4, 2, Rating.Good, wasNew: false),
            Log(5, 3, Rating.Good, wasNew: false),
        ];

        var usage = DailyUsage.FromLogs(logs);

        Assert.Equal(1, usage.NewCardsIntroduced);
        Assert.Equal(2, usage.ReviewCardsDone);
        Assert.Equal(3, usage.CardsReviewed);
        Assert.Equal(5, usage.Ratings);
    }

    [Fact]
    public void Cards_beyond_limit_are_not_lost_and_appear_the_next_day()
    {
        var time = new FakeTimeProvider(2026, 10, 15);
        var store = new InMemoryReviewStore(Enumerable.Range(1, 25).Select(i => TestCards.New(i)));
        var limits = new DailyLimits(20, 100);

        // 第 1 天：只出現 20 張新卡，全部評普通
        var day1 = time.Today();
        var queue1 = ReviewQueueBuilder.Build(store.Cards.Values, DailyUsage.FromLogs(store.LogsOn(day1)), limits, day1);
        Assert.Equal(20, queue1.Count);
        var session = new ReviewSession(queue1, store, time);
        while (!session.IsFinished)
        {
            session.Rate(Rating.Good);
        }

        // 同一天再開一次：已達新卡上限，沒有卡片
        var again = ReviewQueueBuilder.Build(store.Cards.Values, DailyUsage.FromLogs(store.LogsOn(day1)), limits, day1);
        Assert.Empty(again);

        // 第 2 天：前一天的 20 張到期（interval=1）+ 剩下 5 張新卡
        time.AdvanceDays(1);
        var day2 = time.Today();
        var queue2 = ReviewQueueBuilder.Build(store.Cards.Values, DailyUsage.FromLogs(store.LogsOn(day2)), limits, day2);
        Assert.Equal(20, queue2.Count(c => !c.IsNew));
        Assert.Equal([21L, 22L, 23L, 24L, 25L], queue2.Where(c => c.IsNew).Select(c => c.Id));
    }

    [Fact]
    public void Overdue_reviews_beyond_limit_carry_over_to_next_day()
    {
        var cards = Enumerable.Range(1, 120).Select(i => TestCards.Due(i, Today)).ToList();
        var limits = new DailyLimits(20, 100);
        var today = ReviewQueueBuilder.Build(cards, DailyUsage.None, limits, Today);
        var leftover = cards.Except(today).ToList();
        Assert.Equal(20, leftover.Count);

        // 隔天這 20 張仍是到期（逾期）卡片
        var tomorrow = ReviewQueueBuilder.Build(leftover, DailyUsage.None, limits, Today.AddDays(1));
        Assert.Equal(20, tomorrow.Count);
    }
}
