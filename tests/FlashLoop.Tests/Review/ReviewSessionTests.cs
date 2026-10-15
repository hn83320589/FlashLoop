using FlashLoop.Core.Model;
using FlashLoop.Core.Review;
using FlashLoop.Core.Scheduling;
using FlashLoop.Core.Time;
using FlashLoop.Tests.Support;

namespace FlashLoop.Tests.Review;

public class ReviewSessionTests
{
    private readonly FakeTimeProvider _time = new(2026, 10, 15);

    private (ReviewSession Session, InMemoryReviewStore Store) Start(params Card[] cards)
    {
        var store = new InMemoryReviewStore(cards);
        return (new ReviewSession(cards, store, _time), store);
    }

    private static List<long> Drain(ReviewSession session, Func<Card, Rating> rate)
    {
        var shown = new List<long>();
        while (!session.IsFinished)
        {
            var card = session.Current!;
            shown.Add(card.Id);
            session.Rate(rate(card));
        }
        return shown;
    }

    [Fact]
    public void Rating_persists_card_and_log_immediately()
    {
        var (session, store) = Start(TestCards.New(1));
        session.Rate(Rating.Good);

        var saved = store.Cards[1];
        Assert.Equal(1, saved.Schedule.Interval);
        Assert.Equal(_time.Today().AddDays(1), saved.Schedule.DueDate);
        Assert.False(saved.IsNew);
        var log = Assert.Single(store.Logs);
        Assert.True(log.WasNew);
        Assert.Equal(Rating.Good, log.Rating);
        Assert.Equal(_time.Today(), log.ReviewDate);
    }

    [Fact]
    public void Again_card_reappears_at_end_until_rated_good_or_better()
    {
        var (session, _) = Start(TestCards.New(1), TestCards.New(2), TestCards.New(3));
        var againCount = 0;

        var shown = Drain(session, c =>
        {
            if (c.Id == 1 && againCount < 2)
            {
                againCount++;
                return Rating.Again;
            }
            return Rating.Good;
        });

        Assert.Equal([1L, 2L, 3L, 1L, 1L], shown);
    }

    [Fact]
    public void Card_rated_good_after_again_does_not_reappear()
    {
        var (session, _) = Start(TestCards.New(1), TestCards.New(2));
        session.Rate(Rating.Again); // 1 -> 隊尾
        session.Rate(Rating.Good);  // 2
        Assert.Equal(1, session.Current!.Id);
        session.Rate(Rating.Good);  // 1 完成
        Assert.True(session.IsFinished);
    }

    [Fact]
    public void Hard_on_relearning_card_keeps_it_in_session()
    {
        var (session, _) = Start(TestCards.New(1));
        var shown = new List<Rating> { Rating.Again, Rating.Hard, Rating.Easy };
        var i = 0;
        var ids = Drain(session, _ => shown[i++]);
        Assert.Equal([1L, 1L, 1L], ids);
    }

    [Fact]
    public void Hard_on_normal_card_does_not_requeue()
    {
        var (session, _) = Start(TestCards.Due(1, _time.Today()));
        session.Rate(Rating.Hard);
        Assert.True(session.IsFinished);
    }

    [Fact]
    public void Requeued_card_uses_schedule_from_previous_rating()
    {
        var (session, store) = Start(TestCards.Due(1, _time.Today(), interval: 15));
        session.Rate(Rating.Again);
        Assert.Equal(0, session.Current!.Schedule.Repetitions);
        session.Rate(Rating.Good);
        Assert.Equal(1, store.Cards[1].Schedule.Interval);
        Assert.Equal(1, store.Cards[1].Schedule.Repetitions);
        Assert.Equal(2.3, store.Cards[1].Schedule.Ease);
    }

    [Fact]
    public void Same_card_never_shown_twice_in_a_row_when_others_remain()
    {
        var cards = Enumerable.Range(1, 5).Select(i => TestCards.New(i)).ToArray();
        var (session, _) = Start(cards);
        var rng = new Random(42);
        var shown = Drain(session, _ => rng.Next(3) == 0 ? Rating.Again : Rating.Good);

        for (var i = 1; i < shown.Count; i++)
        {
            if (shown[i] == shown[i - 1])
            {
                // 只允許在佇列只剩這張重來卡時連續出現
                Assert.DoesNotContain(shown.Skip(i + 1), id => id != shown[i]);
            }
        }
    }

    [Fact]
    public void Undo_restores_card_schedule_and_removes_log()
    {
        var original = TestCards.Due(1, _time.Today(), interval: 6);
        var (session, store) = Start(original, TestCards.New(2));
        var usageBefore = DailyUsage.FromLogs(store.LogsOn(_time.Today()));

        session.Rate(Rating.Easy);
        Assert.NotEqual(original, store.Cards[1]);

        Assert.True(session.Undo());

        Assert.Equal(original, store.Cards[1]);
        Assert.Empty(store.Logs);
        Assert.Equal(usageBefore, DailyUsage.FromLogs(store.LogsOn(_time.Today())));
        Assert.Equal(1, session.Current!.Id);
        Assert.Equal(2, session.Remaining);
    }

    [Fact]
    public void Undo_of_again_removes_requeued_copy()
    {
        var (session, store) = Start(TestCards.New(1), TestCards.New(2));
        session.Rate(Rating.Again);
        Assert.Equal(2, session.Remaining);

        session.Undo();

        Assert.Equal(2, session.Remaining);
        Assert.Equal([1L, 2L], session.Queue.Select(c => c.Id));
        Assert.True(store.Cards[1].IsNew);
        session.Rate(Rating.Good);
        session.Rate(Rating.Good);
        Assert.True(session.IsFinished);
    }

    [Fact]
    public void Undo_after_session_finished_brings_last_card_back()
    {
        var (session, _) = Start(TestCards.New(1));
        session.Rate(Rating.Good);
        Assert.True(session.IsFinished);
        session.Undo();
        Assert.False(session.IsFinished);
        Assert.Equal(1, session.Current!.Id);
    }

    [Fact]
    public void Undo_keeps_at_most_10_entries()
    {
        var cards = Enumerable.Range(1, 12).Select(i => TestCards.New(i)).ToArray();
        var (session, store) = Start(cards);
        for (var i = 0; i < 12; i++)
        {
            session.Rate(Rating.Good);
        }

        var undone = 0;
        while (session.Undo())
        {
            undone++;
        }

        Assert.Equal(10, undone);
        Assert.Equal(2, store.Logs.Count);
        Assert.False(store.Cards[2].IsNew);
        Assert.True(store.Cards[3].IsNew);
    }

    [Fact]
    public void Undo_with_nothing_to_undo_returns_false()
    {
        var (session, _) = Start(TestCards.New(1));
        Assert.False(session.CanUndo);
        Assert.False(session.Undo());
    }

    [Fact]
    public void Undo_restores_relearning_state()
    {
        var (session, _) = Start(TestCards.New(1), TestCards.New(2));
        session.Rate(Rating.Again); // 1 relearning
        session.Rate(Rating.Good);  // 2 done
        session.Rate(Rating.Good);  // 1 done (leaves relearning)
        session.Undo();             // 1 回到 relearning
        Assert.Equal(1, session.Current!.Id);
        session.Rate(Rating.Hard);  // 仍在 relearning -> 會再出現
        Assert.False(session.IsFinished);
        Assert.Equal(1, session.Current!.Id);
    }

    [Fact]
    public void Summary_counts_ratings_and_distinct_cards_and_follows_undo()
    {
        var (session, _) = Start(TestCards.New(1), TestCards.New(2));
        session.Rate(Rating.Again);
        session.Rate(Rating.Easy);
        session.Rate(Rating.Good);

        var s = session.Summary;
        Assert.Equal(1, s.Again);
        Assert.Equal(1, s.Good);
        Assert.Equal(1, s.Easy);
        Assert.Equal(3, s.TotalRatings);
        Assert.Equal(2, s.DistinctCards);

        session.Undo();
        session.Undo();
        Assert.Equal(1, session.Summary.TotalRatings);
        Assert.Equal(1, session.Summary.DistinctCards);
    }

    [Fact]
    public void Rate_when_finished_throws()
    {
        var (session, _) = Start();
        Assert.True(session.IsFinished);
        Assert.Throws<InvalidOperationException>(() => session.Rate(Rating.Good));
    }
}
