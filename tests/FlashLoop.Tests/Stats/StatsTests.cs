using FlashLoop.Core.Model;
using FlashLoop.Core.Stats;
using FlashLoop.Tests.Support;

namespace FlashLoop.Tests.Stats;

public class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static DateOnly D(int offset) => Today.AddDays(offset);

    [Fact]
    public void No_reviews_means_zero() =>
        Assert.Equal(0, StreakCalculator.Calculate([], Today));

    [Fact]
    public void Only_today_counts_as_one() =>
        Assert.Equal(1, StreakCalculator.Calculate([D(0)], Today));

    [Fact]
    public void Consecutive_days_including_today() =>
        Assert.Equal(4, StreakCalculator.Calculate([D(-3), D(-2), D(-1), D(0)], Today));

    [Fact]
    public void Multiple_reviews_on_same_day_count_once() =>
        Assert.Equal(2, StreakCalculator.Calculate([D(-1), D(-1), D(0), D(0), D(0)], Today));

    [Fact]
    public void Not_yet_reviewed_today_keeps_streak_from_yesterday() =>
        Assert.Equal(3, StreakCalculator.Calculate([D(-3), D(-2), D(-1)], Today));

    [Fact]
    public void Missing_yesterday_and_today_resets_to_zero() =>
        Assert.Equal(0, StreakCalculator.Calculate([D(-4), D(-3), D(-2)], Today));

    [Fact]
    public void Gap_of_one_day_breaks_streak() =>
        Assert.Equal(2, StreakCalculator.Calculate([D(-5), D(-4), D(-3), D(-1), D(0)], Today));

    [Fact]
    public void Future_dates_are_ignored() =>
        Assert.Equal(1, StreakCalculator.Calculate([D(0), D(1), D(2)], Today));
}

public class ForecastCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Fact]
    public void Returns_14_days_starting_today()
    {
        var f = ForecastCalculator.Forecast([], Today);
        Assert.Equal(14, f.Count);
        Assert.Equal(Today, f[0].Date);
        Assert.Equal(Today.AddDays(13), f[13].Date);
        Assert.All(f, d => Assert.Equal(0, d.Count));
    }

    [Fact]
    public void Today_includes_overdue_cards_and_new_cards_are_excluded()
    {
        Card[] cards =
        [
            TestCards.Due(1, Today.AddDays(-5)),
            TestCards.Due(2, Today),
            TestCards.Due(3, Today.AddDays(1)),
            TestCards.Due(4, Today.AddDays(1)),
            TestCards.Due(5, Today.AddDays(13)),
            TestCards.Due(6, Today.AddDays(14)), // 超出範圍
            TestCards.New(7),
        ];

        var f = ForecastCalculator.Forecast(cards, Today);

        Assert.Equal(2, f[0].Count);
        Assert.Equal(2, f[1].Count);
        Assert.Equal(1, f[13].Count);
        Assert.Equal(5, f.Sum(d => d.Count));
    }
}
