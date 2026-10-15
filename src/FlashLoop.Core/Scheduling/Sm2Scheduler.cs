namespace FlashLoop.Core.Scheduling;

/// <summary>
/// SM-2 變體排程。純函式：給定目前狀態、評分與評分當日，回傳新的排程狀態。
/// 所有四捨五入皆採 MidpointRounding.AwayFromZero；ease 保留兩位小數以避免浮點誤差累積。
/// </summary>
public static class Sm2Scheduler
{
    private const double AgainEasePenalty = 0.2;
    private const double HardEasePenalty = 0.15;
    private const double EasyEaseBonus = 0.15;
    private const double HardIntervalFactor = 1.2;
    private const double EasyIntervalFactor = 1.3;
    private const int EasyMinIntervalForFirstRepetition = 4;

    /// <summary>
    /// 間隔上限（約 100 年）。規格未定上限，此值實務上碰不到，用來防止連續評「簡單」造成 int 溢位。
    /// </summary>
    public const int MaxInterval = 36500;

    public static CardSchedule Apply(CardSchedule current, Rating rating, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(current);

        var (ease, repetitions, interval) = rating switch
        {
            Rating.Again => (ClampEase(current.Ease - AgainEasePenalty), 0, 1),
            Rating.Hard => (
                ClampEase(current.Ease - HardEasePenalty),
                current.Repetitions,
                Math.Max(1, RoundInterval(current.Interval * HardIntervalFactor))),
            Rating.Good => (current.Ease, current.Repetitions + 1, GoodInterval(current)),
            Rating.Easy => EasyResult(current),
            _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, "評分必須是 1 到 4"),
        };

        return new CardSchedule(ease, repetitions, interval, today.AddDays(interval));
    }

    private static int GoodInterval(CardSchedule s) => s.Repetitions switch
    {
        0 => 1,
        1 => 6,
        _ => RoundInterval(s.Interval * s.Ease),
    };

    private static (double Ease, int Repetitions, int Interval) EasyResult(CardSchedule s)
    {
        var ease = RoundEase(s.Ease + EasyEaseBonus);
        var interval = RoundInterval(s.Interval * ease * EasyIntervalFactor);
        if (s.Repetitions == 0)
        {
            interval = Math.Max(EasyMinIntervalForFirstRepetition, interval);
        }
        return (ease, s.Repetitions + 1, interval);
    }

    private static double ClampEase(double ease) => Math.Max(CardSchedule.MinEase, RoundEase(ease));

    private static double RoundEase(double ease) => Math.Round(ease, 2, MidpointRounding.AwayFromZero);

    // 先在 double 範圍內套用上限再轉 int，避免超出 int 範圍時轉型溢位
    private static int RoundInterval(double days) =>
        (int)Math.Round(Math.Min(days, MaxInterval), MidpointRounding.AwayFromZero);
}
