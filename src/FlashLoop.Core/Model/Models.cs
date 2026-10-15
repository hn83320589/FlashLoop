using FlashLoop.Core.Scheduling;

namespace FlashLoop.Core.Model;

public sealed record Deck(long Id, Guid Guid, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record Card
{
    public long Id { get; init; }
    public Guid Guid { get; init; }
    public long DeckId { get; init; }
    public required string Front { get; init; }
    public required string Back { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public CardSchedule Schedule { get; init; } = CardSchedule.New;

    /// <summary>最後一次評分時間；null 表示從未評分（新卡）。</summary>
    public DateTimeOffset? LastReviewedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public bool IsNew => LastReviewedAt is null;

    public bool IsDueOn(DateOnly day) => !IsNew && Schedule.DueDate is { } due && due <= day;
}

/// <summary>一次評分紀錄。ReviewDate 為評分當下的本地日期，用於每日統計與連續天數。</summary>
public sealed record ReviewLog(
    long Id,
    Guid Guid,
    long CardId,
    DateTimeOffset ReviewedAt,
    DateOnly ReviewDate,
    Rating Rating,
    bool WasNew);

public sealed record DailyLimits(int NewCardsPerDay, int ReviewsPerDay)
{
    public static DailyLimits Default { get; } = new(20, 100);
}
