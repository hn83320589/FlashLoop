using FlashLoop.Core.Services;
using FlashLoop.Tests.Support;

namespace FlashLoop.Tests.Data;

public class DeckPersistenceTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Create_rename_delete_and_counts_survive_reopen()
    {
        var n2 = _db.DeckWithCsv("N2 單字", "勉強,べんきょう\n試験,しけん\n経験,けいけん\n");
        var other = _db.Decks.Create("其他");
        _db.Decks.Rename(n2.Id, "N2 動詞");
        var temp = _db.Decks.Create("暫存");
        _db.Decks.Delete(temp.Id);

        _db.Reopen();

        var overviews = _db.Decks.GetOverviews();
        Assert.Equal(["N2 動詞", "其他"], overviews.Select(o => o.Deck.Name));
        var n2Row = overviews.Single(o => o.Deck.Id == n2.Id);
        Assert.Equal(3, n2Row.TotalCards);
        Assert.Equal(3, n2Row.NewToday);
        Assert.Equal(0, n2Row.DueToday);
        Assert.Equal(0, overviews.Single(o => o.Deck.Id == other.Id).TotalCards);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_is_rejected(string name) =>
        Assert.Throws<ValidationException>(() => _db.Decks.Create(name));

    [Fact]
    public void Duplicate_name_is_rejected_case_insensitively_for_create_and_rename()
    {
        _db.Decks.Create("Verbs");
        var b = _db.Decks.Create("Nouns");
        Assert.Throws<ValidationException>(() => _db.Decks.Create(" verbs "));
        Assert.Throws<ValidationException>(() => _db.Decks.Rename(b.Id, "VERBS"));
        _db.Decks.Rename(b.Id, "nouns"); // 只改大小寫是允許的
        Assert.Contains(_db.Decks.GetOverviews(), o => o.Deck.Name == "nouns");
    }

    [Fact]
    public void Delete_removes_cards_and_review_logs()
    {
        var deck = _db.DeckWithCsv("A", "a,b\nc,d\n");
        var session = _db.Review.Start(deck.Id);
        session.Rate(Core.Scheduling.Rating.Good);

        _db.Decks.Delete(deck.Id);

        using var c = _db.OpenRaw();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT (SELECT COUNT(*) FROM cards) + (SELECT COUNT(*) FROM review_logs)";
        Assert.Equal(0L, (long)cmd.ExecuteScalar()!);
    }

    [Fact]
    public void Overview_counts_respect_global_daily_limits()
    {
        _db.Settings.SaveLimits(new Core.Model.DailyLimits(NewCardsPerDay: 2, ReviewsPerDay: 100));
        var a = _db.DeckWithCsv("A", "a1,x\na2,x\na3,x\n");
        var b = _db.DeckWithCsv("B", "b1,x\n");

        var session = _db.Review.Start(a.Id);
        session.Rate(Core.Scheduling.Rating.Good);

        var overviews = _db.Decks.GetOverviews();
        Assert.Equal(1, overviews.Single(o => o.Deck.Id == a.Id).NewToday);
        Assert.Equal(1, overviews.Single(o => o.Deck.Id == b.Id).NewToday);
        Assert.Equal(3, overviews.Single(o => o.Deck.Id == a.Id).TotalCards);
    }
}
