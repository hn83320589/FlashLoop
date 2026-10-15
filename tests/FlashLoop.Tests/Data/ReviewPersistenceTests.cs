using System.Text;
using FlashLoop.Core.Model;
using FlashLoop.Core.Review;
using FlashLoop.Core.Scheduling;
using FlashLoop.Core.Time;
using FlashLoop.Tests.Support;

namespace FlashLoop.Tests.Data;

public class ReviewPersistenceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Import_through_service_reports_summary_and_skips_existing_on_second_import()
    {
        var deck = _db.Decks.Create("N2");
        var bytes = Encoding.UTF8.GetBytes("正面,背面,標籤\n勉強,べんきょう,N2;名詞\n試験,しけん\n壊れた\n");

        var first = _db.Import.Import(deck.Id, bytes);
        Assert.Equal(2, first.Added);
        Assert.Equal(4, first.Errors.Single().LineNumber);

        var second = _db.Import.Import(deck.Id, bytes);
        Assert.Equal(0, second.Added);
        Assert.Equal(2, second.Skipped);

        var cards = _db.Store.GetCardsByDeck(deck.Id);
        Assert.Equal(2, cards.Count);
        Assert.Equal(["N2", "名詞"], cards[0].Tags);
    }

    [Fact]
    public void Rating_is_persisted_and_survives_reopen()
    {
        var deck = _db.DeckWithCsv("A", "a,b\n");
        _db.Review.Start(deck.Id).Rate(Rating.Easy);

        _db.Reopen();

        var card = _db.Store.GetCardsByDeck(deck.Id).Single();
        Assert.Equal(new CardSchedule(2.65, 1, 4, _db.Time.Today().AddDays(4)), card.Schedule);
        Assert.False(card.IsNew);
        Assert.Single(_db.Store.GetLogsOn(_db.Time.Today()));
    }

    [Fact]
    public void Undo_restores_card_row_and_today_statistics_in_database()
    {
        var deck = _db.DeckWithCsv("A", "a,b\nc,d\n");
        var session = _db.Review.Start(deck.Id);
        session.Rate(Rating.Good);

        var before = _db.Store.GetCardsByDeck(deck.Id).Single(c => c.Front == "c");
        var statsBefore = _db.Stats.Get();
        var usageBefore = DailyUsage.FromLogs(_db.Store.GetLogsOn(_db.Time.Today()));

        _db.Time.Advance(TimeSpan.FromMinutes(1));
        session.Rate(Rating.Again);
        Assert.NotEqual(before, _db.Store.GetCardsByDeck(deck.Id).Single(c => c.Front == "c"));
        Assert.Equal(2, _db.Stats.Get().CardsReviewedToday);

        session.Undo();

        Assert.Equal(before, _db.Store.GetCardsByDeck(deck.Id).Single(c => c.Front == "c"));
        Assert.Equal(usageBefore, DailyUsage.FromLogs(_db.Store.GetLogsOn(_db.Time.Today())));
        var statsAfter = _db.Stats.Get();
        Assert.Equal(statsBefore.CardsReviewedToday, statsAfter.CardsReviewedToday);
        Assert.Equal(statsBefore.RatingsToday, statsAfter.RatingsToday);
        Assert.Equal(statsBefore.Streak, statsAfter.Streak);
        Assert.Equal(statsBefore.Forecast, statsAfter.Forecast);
    }

    [Fact]
    public void Undo_of_first_review_of_the_day_resets_streak_and_new_card_quota()
    {
        _db.Settings.SaveLimits(new DailyLimits(1, 100));
        var deck = _db.DeckWithCsv("A", "a,b\nc,d\n");
        var session = _db.Review.Start(deck.Id);
        session.Rate(Rating.Good);
        Assert.Equal(1, _db.Stats.Get().Streak);
        Assert.Equal(0, _db.Decks.GetOverviews().Single().NewToday);

        session.Undo();

        Assert.Equal(0, _db.Stats.Get().Streak);
        Assert.Equal(1, _db.Decks.GetOverviews().Single().NewToday);
    }

    [Fact]
    public void Forecast_matches_raw_due_counts_in_database()
    {
        var csv = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"w{i},m{i}"));
        var deck = _db.DeckWithCsv("A", csv);

        // 在不同日子以不同評分學習，讓到期日分散
        var ratings = new[] { Rating.Good, Rating.Easy, Rating.Hard, Rating.Good };
        for (var day = 0; day < 4; day++)
        {
            var session = _db.Review.Start(deck.Id);
            var i = 0;
            while (!session.IsFinished)
            {
                session.Rate(ratings[(i++ + day) % ratings.Length]);
            }
            _db.Time.AdvanceDays(1);
        }

        var today = _db.Time.Today();
        var forecast = _db.Stats.Get().Forecast;

        using var c = _db.OpenRaw();
        for (var offset = 0; offset < 14; offset++)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = offset == 0
                ? "SELECT COUNT(*) FROM cards WHERE last_reviewed_at IS NOT NULL AND due_date <= $d"
                : "SELECT COUNT(*) FROM cards WHERE last_reviewed_at IS NOT NULL AND due_date = $d";
            cmd.Parameters.AddWithValue("$d", today.AddDays(offset).ToString("yyyy-MM-dd"));
            Assert.Equal((long)cmd.ExecuteScalar()!, forecast[offset].Count);
        }
        Assert.True(forecast.Sum(f => f.Count) > 0);
    }

    [Fact]
    public void Streak_counts_days_with_reviews_across_sessions()
    {
        var deck = _db.DeckWithCsv("A", string.Join('\n', Enumerable.Range(1, 10).Select(i => $"w{i},m{i}")));
        _db.Settings.SaveLimits(new DailyLimits(2, 100));

        for (var day = 0; day < 3; day++)
        {
            var s = _db.Review.Start(deck.Id);
            s.Rate(Rating.Good);
            _db.Review.Start(deck.Id).Rate(Rating.Good); // 同一天第二次複習
            _db.Time.AdvanceDays(1);
        }
        Assert.Equal(3, _db.Stats.Get().Streak); // 今天尚未複習，保留昨天的連續

        _db.Time.AdvanceDays(1); // 中斷一天
        Assert.Equal(0, _db.Stats.Get().Streak);
    }

    [Fact]
    public void Settings_default_when_missing_and_roundtrip()
    {
        Assert.Equal(DailyLimits.Default, _db.Settings.LoadLimits());
        _db.Settings.SaveLimits(new DailyLimits(5, 50));
        Assert.Equal(new DailyLimits(5, 50), _db.Settings.LoadLimits());
    }
}
