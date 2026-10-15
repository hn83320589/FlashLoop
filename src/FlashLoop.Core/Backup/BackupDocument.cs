namespace FlashLoop.Core.Backup;

/// <summary>備份檔格式。所有關聯以 Guid 表示，與資料庫 Id 無關，因此可跨機器還原與合併。</summary>
public sealed record BackupDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public DateTimeOffset ExportedAt { get; init; }
    public BackupSettings? Settings { get; init; }
    public IReadOnlyList<BackupDeck> Decks { get; init; } = [];
    public IReadOnlyList<BackupCard> Cards { get; init; } = [];
    public IReadOnlyList<BackupReviewLog> ReviewLogs { get; init; } = [];
}

public sealed record BackupSettings(int NewCardsPerDay, int ReviewsPerDay);

public sealed record BackupDeck(Guid Guid, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record BackupCard(
    Guid Guid,
    Guid DeckGuid,
    string Front,
    string Back,
    IReadOnlyList<string> Tags,
    double Ease,
    int Repetitions,
    int Interval,
    DateOnly? DueDate,
    DateTimeOffset? LastReviewedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BackupReviewLog(
    Guid Guid,
    Guid CardGuid,
    DateTimeOffset ReviewedAt,
    DateOnly ReviewDate,
    int Rating,
    bool WasNew);
