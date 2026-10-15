using FlashLoop.Core.Scheduling;

namespace FlashLoop.Tests.Scheduling;

public class Sm2SchedulerTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Fact]
    public void New_card_has_initial_state()
    {
        var s = CardSchedule.New;
        Assert.Equal(2.5, s.Ease);
        Assert.Equal(0, s.Repetitions);
        Assert.Equal(0, s.Interval);
        Assert.Null(s.DueDate);
    }

    [Fact]
    public void Consecutive_good_follows_1_6_then_interval_times_ease()
    {
        var s = CardSchedule.New;
        var intervals = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            s = Sm2Scheduler.Apply(s, Rating.Good, Today);
            intervals.Add(s.Interval);
        }

        // 1, 6, round(6*2.5)=15, round(15*2.5)=round(37.5)=38, round(38*2.5)=95
        Assert.Equal([1, 6, 15, 38, 95], intervals);
        Assert.Equal(5, s.Repetitions);
        Assert.Equal(2.5, s.Ease);
    }

    [Fact]
    public void Due_date_is_today_plus_interval()
    {
        var s = Sm2Scheduler.Apply(CardSchedule.New, Rating.Good, Today);
        s = Sm2Scheduler.Apply(s, Rating.Good, Today);
        Assert.Equal(Today.AddDays(6), s.DueDate);
    }

    [Fact]
    public void Again_lowers_ease_resets_repetitions_and_sets_interval_1()
    {
        var s = new CardSchedule(2.5, 3, 15, Today);
        var r = Sm2Scheduler.Apply(s, Rating.Again, Today);
        Assert.Equal(2.3, r.Ease);
        Assert.Equal(0, r.Repetitions);
        Assert.Equal(1, r.Interval);
        Assert.Equal(Today.AddDays(1), r.DueDate);
    }

    [Theory]
    [InlineData(1.3, 1.3)]
    [InlineData(1.4, 1.3)]
    [InlineData(1.5, 1.3)]
    [InlineData(1.6, 1.4)]
    public void Again_ease_never_below_1_3(double ease, double expected)
    {
        var r = Sm2Scheduler.Apply(new CardSchedule(ease, 2, 6, Today), Rating.Again, Today);
        Assert.Equal(expected, r.Ease);
    }

    [Fact]
    public void Hard_lowers_ease_and_multiplies_interval_by_1_2_keeping_repetitions()
    {
        var r = Sm2Scheduler.Apply(new CardSchedule(2.5, 2, 10, Today), Rating.Hard, Today);
        Assert.Equal(2.35, r.Ease);
        Assert.Equal(12, r.Interval);
        Assert.Equal(2, r.Repetitions);
        Assert.Equal(Today.AddDays(12), r.DueDate);
    }

    [Theory]
    [InlineData(0, 1)] // max(1, 0)
    [InlineData(1, 1)] // round(1.2)=1
    [InlineData(2, 2)] // round(2.4)=2
    [InlineData(3, 4)] // round(3.6)=4
    [InlineData(5, 6)] // round(6.0)=6
    public void Hard_interval_is_at_least_1(int interval, int expected)
    {
        var r = Sm2Scheduler.Apply(new CardSchedule(2.5, 0, interval, null), Rating.Hard, Today);
        Assert.Equal(expected, r.Interval);
    }

    [Theory]
    [InlineData(1.3, 1.3)]
    [InlineData(1.4, 1.3)]
    [InlineData(1.45, 1.3)]
    [InlineData(1.5, 1.35)]
    public void Hard_ease_never_below_1_3(double ease, double expected)
    {
        var r = Sm2Scheduler.Apply(new CardSchedule(ease, 1, 6, Today), Rating.Hard, Today);
        Assert.Equal(expected, r.Ease);
    }

    [Fact]
    public void Easy_on_new_card_gives_at_least_4_days()
    {
        var r = Sm2Scheduler.Apply(CardSchedule.New, Rating.Easy, Today);
        Assert.Equal(2.65, r.Ease);
        Assert.Equal(4, r.Interval);
        Assert.Equal(1, r.Repetitions);
        Assert.Equal(Today.AddDays(4), r.DueDate);
    }

    [Fact]
    public void Easy_uses_increased_ease_and_1_3_bonus()
    {
        // ease 2.5 -> 2.65; round(6 * 2.65 * 1.3) = round(20.67) = 21
        var r = Sm2Scheduler.Apply(new CardSchedule(2.5, 2, 6, Today), Rating.Easy, Today);
        Assert.Equal(2.65, r.Ease);
        Assert.Equal(21, r.Interval);
        Assert.Equal(3, r.Repetitions);
    }

    [Fact]
    public void Easy_after_again_with_zero_repetitions_still_at_least_4()
    {
        // reps 0, interval 1, ease 1.3 -> 1.45; round(1 * 1.45 * 1.3)=2 -> min 4
        var r = Sm2Scheduler.Apply(new CardSchedule(1.3, 0, 1, Today), Rating.Easy, Today);
        Assert.Equal(1.45, r.Ease);
        Assert.Equal(4, r.Interval);
    }

    [Fact]
    public void Easy_with_repetitions_above_zero_has_no_minimum()
    {
        // reps 1, interval 1, ease 1.3 -> 1.45; round(1.885)=2
        var r = Sm2Scheduler.Apply(new CardSchedule(1.3, 1, 1, Today), Rating.Easy, Today);
        Assert.Equal(2, r.Interval);
    }

    [Fact]
    public void Good_after_again_restarts_at_1_then_6()
    {
        var s = new CardSchedule(2.5, 4, 40, Today);
        s = Sm2Scheduler.Apply(s, Rating.Again, Today);
        s = Sm2Scheduler.Apply(s, Rating.Good, Today);
        Assert.Equal(1, s.Interval);
        Assert.Equal(1, s.Repetitions);
        s = Sm2Scheduler.Apply(s, Rating.Good, Today);
        Assert.Equal(6, s.Interval);
        s = Sm2Scheduler.Apply(s, Rating.Good, Today);
        Assert.Equal(14, s.Interval); // round(6 * 2.3) = round(13.8)
    }

    [Fact]
    public void Good_does_not_change_ease()
    {
        var r = Sm2Scheduler.Apply(new CardSchedule(1.7, 2, 6, Today), Rating.Good, Today);
        Assert.Equal(1.7, r.Ease);
        Assert.Equal(10, r.Interval); // round(10.2)
    }

    [Fact]
    public void Rounding_uses_midpoint_away_from_zero()
    {
        // round(5 * 2.5) = round(12.5) -> 13 (banker's rounding would give 12)
        var r = Sm2Scheduler.Apply(new CardSchedule(2.5, 2, 5, Today), Rating.Good, Today);
        Assert.Equal(13, r.Interval);
    }

    [Fact]
    public void Repeated_again_keeps_ease_at_floor()
    {
        var s = CardSchedule.New;
        for (var i = 0; i < 20; i++)
        {
            s = Sm2Scheduler.Apply(s, Rating.Again, Today);
        }
        Assert.Equal(1.3, s.Ease);
        Assert.Equal(1, s.Interval);
    }

    [Theory]
    [InlineData(Rating.Hard)]
    [InlineData(Rating.Good)]
    [InlineData(Rating.Easy)]
    public void Interval_never_exceeds_maximum(Rating rating)
    {
        // 間隔已接近上限時，乘上 ease 或係數不得超過上限，也不得因 int 溢位變成負數
        var s = new CardSchedule(10.0, 50, Sm2Scheduler.MaxInterval - 1, Today);
        for (var i = 0; i < 3; i++)
        {
            s = Sm2Scheduler.Apply(s, rating, Today);
            Assert.Equal(Sm2Scheduler.MaxInterval, s.Interval);
            Assert.Equal(Today.AddDays(Sm2Scheduler.MaxInterval), s.DueDate);
        }
    }

    [Fact]
    public void Huge_stored_interval_does_not_overflow()
    {
        var s = new CardSchedule(2.5, 5, int.MaxValue / 2, Today);
        Assert.Equal(Sm2Scheduler.MaxInterval, Sm2Scheduler.Apply(s, Rating.Easy, Today).Interval);
    }
}
