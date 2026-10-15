using FlashLoop.Core.Csv;
using FlashLoop.Core.Model;

namespace FlashLoop.Core.Persistence;

public interface IDeckRepository
{
    IReadOnlyList<Deck> GetAllDecks();
    Deck? FindDeck(long id);
    bool DeckNameExists(string name, long? excludingId = null);
    Deck AddDeck(string name, DateTimeOffset now);
    void RenameDeck(long id, string name, DateTimeOffset now);

    /// <summary>刪除牌組，連同其卡片與評分紀錄。</summary>
    void DeleteDeck(long id);
}

public interface ICardRepository
{
    IReadOnlyList<Card> GetCardsByDeck(long deckId);
    IReadOnlyList<Card> GetAllCards();
    IReadOnlySet<string> GetFrontKeys(long deckId);

    /// <summary>以單一 transaction 新增卡片，依清單順序給予遞增 Id。</summary>
    void AddCards(long deckId, IEnumerable<ImportedCard> cards, DateTimeOffset now);
}

public interface IReviewLogRepository
{
    IReadOnlyList<ReviewLog> GetLogsOn(DateOnly day);
    IReadOnlyList<DateOnly> GetReviewDates();
}

public interface ISettingsStore
{
    DailyLimits LoadLimits();
    void SaveLimits(DailyLimits limits);
}
