namespace FlashLoop.Core.Scheduling;

/// <summary>卡片的排程狀態。Interval 以天為單位；DueDate 為 null 表示尚未排程（新卡）。</summary>
public sealed record CardSchedule(double Ease, int Repetitions, int Interval, DateOnly? DueDate)
{
    public const double InitialEase = 2.5;
    public const double MinEase = 1.3;

    public static CardSchedule New { get; } = new(InitialEase, 0, 0, null);
}
