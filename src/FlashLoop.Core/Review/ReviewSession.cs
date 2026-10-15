using FlashLoop.Core.Model;
using FlashLoop.Core.Scheduling;
using FlashLoop.Core.Time;

namespace FlashLoop.Core.Review;

public sealed record SessionSummary(int Again, int Hard, int Good, int Easy, int DistinctCards)
{
    public int TotalRatings => Again + Hard + Good + Easy;
}

/// <summary>
/// 一次複習的流程狀態。佇列在建立時固定；評分後立即寫入 store。
/// 評「重來」的卡會排到隊尾，之後只要評分低於「普通」就繼續留在本次佇列。
/// 可撤銷最近 <see cref="MaxUndo"/> 筆評分。
/// </summary>
public sealed class ReviewSession
{
    public const int MaxUndo = 10;

    private readonly List<Card> _queue;
    private readonly IReviewStore _store;
    private readonly TimeProvider _time;
    private readonly HashSet<long> _relearning = [];
    private readonly LinkedList<UndoEntry> _undo = new();
    private readonly Dictionary<long, int> _ratingsPerCard = [];
    private readonly int[] _ratingCounts = new int[5];

    public ReviewSession(IEnumerable<Card> queue, IReviewStore store, TimeProvider time)
    {
        _queue = queue.ToList();
        _store = store;
        _time = time;
    }

    public Card? Current => _queue.Count > 0 ? _queue[0] : null;
    public IReadOnlyList<Card> Queue => _queue;
    public int Remaining => _queue.Count;
    public bool IsFinished => _queue.Count == 0;
    public bool CanUndo => _undo.Count > 0;

    public SessionSummary Summary => new(
        _ratingCounts[(int)Rating.Again],
        _ratingCounts[(int)Rating.Hard],
        _ratingCounts[(int)Rating.Good],
        _ratingCounts[(int)Rating.Easy],
        _ratingsPerCard.Count);

    public void Rate(Rating rating)
    {
        var card = Current ?? throw new InvalidOperationException("本次複習已沒有卡片可評分");

        var now = _time.GetUtcNow();
        var today = _time.Today();
        var updated = card with
        {
            Schedule = Sm2Scheduler.Apply(card.Schedule, rating, today),
            LastReviewedAt = now,
            UpdatedAt = now,
        };
        var log = _store.SaveReview(updated, new ReviewLog(0, Guid.NewGuid(), card.Id, now, today, rating, card.IsNew));

        var wasRelearning = _relearning.Contains(card.Id);
        var requeue = rating == Rating.Again || (wasRelearning && rating < Rating.Good);

        _queue.RemoveAt(0);
        if (requeue)
        {
            _queue.Add(updated);
            _relearning.Add(card.Id);
        }
        else
        {
            _relearning.Remove(card.Id);
        }

        _ratingCounts[(int)rating]++;
        _ratingsPerCard[card.Id] = _ratingsPerCard.GetValueOrDefault(card.Id) + 1;

        _undo.AddLast(new UndoEntry(card, log.Id, rating, requeue, wasRelearning));
        if (_undo.Count > MaxUndo)
        {
            _undo.RemoveFirst();
        }
    }

    public bool Undo()
    {
        if (_undo.Last is not { Value: var entry })
        {
            return false;
        }
        _undo.RemoveLast();

        _store.UndoReview(entry.Previous, entry.LogId);

        if (entry.Requeued)
        {
            // 撤銷為 LIFO，之後加入的卡都已撤銷，所以重排的那一張必定在隊尾
            _queue.RemoveAt(_queue.Count - 1);
        }
        _queue.Insert(0, entry.Previous);

        if (entry.WasRelearning)
        {
            _relearning.Add(entry.Previous.Id);
        }
        else
        {
            _relearning.Remove(entry.Previous.Id);
        }

        _ratingCounts[(int)entry.Rating]--;
        var remaining = _ratingsPerCard[entry.Previous.Id] - 1;
        if (remaining == 0)
        {
            _ratingsPerCard.Remove(entry.Previous.Id);
        }
        else
        {
            _ratingsPerCard[entry.Previous.Id] = remaining;
        }
        return true;
    }

    private sealed record UndoEntry(Card Previous, long LogId, Rating Rating, bool Requeued, bool WasRelearning);
}
