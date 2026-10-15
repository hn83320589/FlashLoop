using FlashLoop.Core.Csv;
using FlashLoop.Core.Model;
using FlashLoop.Core.Persistence;
using FlashLoop.Core.Review;
using FlashLoop.Core.Stats;
using FlashLoop.Core.Time;

namespace FlashLoop.Core.Services;

/// <summary>使用者輸入不合法時丟出，訊息可直接顯示給使用者。</summary>
public sealed class ValidationException(string message) : Exception(message);

/// <summary>牌組列表的一列。DueToday / NewToday 為「現在開始複習會出現的張數」，已套用剩餘的全域每日額度。</summary>
public sealed record DeckOverview(Deck Deck, int TotalCards, int DueToday, int NewToday);

public sealed class DeckService(
    IDeckRepository decks,
    ICardRepository cards,
    IReviewLogRepository logs,
    ISettingsStore settings,
    TimeProvider time)
{
    public const int MaxNameLength = 100;

    public IReadOnlyList<DeckOverview> GetOverviews()
    {
        var today = time.Today();
        var usage = DailyUsage.FromLogs(logs.GetLogsOn(today));
        var limits = settings.LoadLimits();
        var cardsByDeck = cards.GetAllCards().ToLookup(c => c.DeckId);

        return decks.GetAllDecks().Select(deck =>
        {
            var deckCards = cardsByDeck[deck.Id].ToList();
            var queue = ReviewQueueBuilder.Build(deckCards, usage, limits, today);
            return new DeckOverview(deck, deckCards.Count, queue.Count(c => !c.IsNew), queue.Count(c => c.IsNew));
        }).ToList();
    }

    public Deck Create(string name) => decks.AddDeck(ValidateName(name, null), time.GetUtcNow());

    public void Rename(long id, string name) => decks.RenameDeck(id, ValidateName(name, id), time.GetUtcNow());

    public void Delete(long id) => decks.DeleteDeck(id);

    private string ValidateName(string? name, long? id)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new ValidationException("牌組名稱不可空白");
        }
        if (trimmed.Length > MaxNameLength)
        {
            throw new ValidationException($"牌組名稱最多 {MaxNameLength} 個字");
        }
        if (decks.DeckNameExists(trimmed, id))
        {
            throw new ValidationException($"已有名為「{trimmed}」的牌組");
        }
        return trimmed;
    }
}

public sealed class ImportService(ICardRepository cards, TimeProvider time)
{
    /// <summary>匯入 CSV 位元組。編碼錯誤時丟出 InvalidDataException。</summary>
    public ImportSummary Import(long deckId, byte[] csvBytes)
    {
        var analysis = CsvImporter.Analyze(csvBytes, cards.GetFrontKeys(deckId));
        cards.AddCards(deckId, analysis.NewCards, time.GetUtcNow());
        return analysis.Summary;
    }
}

public sealed class ReviewService(
    ICardRepository cards,
    IReviewLogRepository logs,
    IReviewStore store,
    ISettingsStore settings,
    TimeProvider time)
{
    public ReviewSession Start(long deckId)
    {
        var today = time.Today();
        var queue = ReviewQueueBuilder.Build(
            cards.GetCardsByDeck(deckId),
            DailyUsage.FromLogs(logs.GetLogsOn(today)),
            settings.LoadLimits(),
            today);
        return new ReviewSession(queue, store, time);
    }
}

public sealed record StatsSnapshot(DateOnly Today, int CardsReviewedToday, int RatingsToday, int Streak, IReadOnlyList<ForecastDay> Forecast);

public sealed class StatsService(ICardRepository cards, IReviewLogRepository logs, TimeProvider time)
{
    public StatsSnapshot Get()
    {
        var today = time.Today();
        var usage = DailyUsage.FromLogs(logs.GetLogsOn(today));
        return new StatsSnapshot(
            today,
            usage.CardsReviewed,
            usage.Ratings,
            StreakCalculator.Calculate(logs.GetReviewDates(), today),
            ForecastCalculator.Forecast(cards.GetAllCards(), today));
    }
}
