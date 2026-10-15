using FlashLoop.Core.Model;
using FlashLoop.Core.Review;

namespace FlashLoop.Tests.Support;

public sealed class InMemoryReviewStore : IReviewStore
{
    private long _nextLogId = 1;

    public Dictionary<long, Card> Cards { get; } = [];
    public List<ReviewLog> Logs { get; } = [];

    public InMemoryReviewStore(IEnumerable<Card>? cards = null)
    {
        foreach (var c in cards ?? [])
        {
            Cards[c.Id] = c;
        }
    }

    public ReviewLog SaveReview(Card updated, ReviewLog log)
    {
        Cards[updated.Id] = updated;
        var saved = log with { Id = _nextLogId++ };
        Logs.Add(saved);
        return saved;
    }

    public void UndoReview(Card previous, long logId)
    {
        Cards[previous.Id] = previous;
        Logs.RemoveAll(l => l.Id == logId);
    }

    public IEnumerable<ReviewLog> LogsOn(DateOnly day) => Logs.Where(l => l.ReviewDate == day);
}

public static class TestCards
{
    public static Card New(long id, long deckId = 1) => new()
    {
        Id = id,
        Guid = Guid.NewGuid(),
        DeckId = deckId,
        Front = $"front{id}",
        Back = $"back{id}",
    };

    public static Card Due(long id, DateOnly due, int interval = 6, long deckId = 1) => New(id, deckId) with
    {
        Schedule = new Core.Scheduling.CardSchedule(2.5, 2, interval, due),
        LastReviewedAt = new DateTimeOffset(due.AddDays(-interval).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
    };
}
