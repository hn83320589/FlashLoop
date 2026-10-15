using FlashLoop.Core.Backup;
using FlashLoop.Core.Model;
using FlashLoop.Core.Scheduling;
using FlashLoop.Tests.Support;

namespace FlashLoop.Tests.Backup;

public class BackupServiceTests : IDisposable
{
    private readonly TestDatabase _source = new();
    private readonly TestDatabase _target = new();

    public void Dispose()
    {
        _source.Dispose();
        _target.Dispose();
    }

    private static BackupService ServiceFor(TestDatabase db) => new(db.Store, db.Settings, db.Time);

    private static string Normalized(BackupDocument doc) =>
        BackupSerializer.Serialize(doc with { ExportedAt = DateTimeOffset.UnixEpoch, Settings = null });

    private void SeedSource()
    {
        _source.Settings.SaveLimits(new DailyLimits(7, 70));
        var deck = _source.DeckWithCsv("N2", "勉強,べんきょう,N2;名詞\n\"多行\n正面\",\"含,逗號與\"\"引號\"\"\"\n試験,しけん\n");
        _source.DeckWithCsv("空牌組", "");
        var session = _source.Review.Start(deck.Id);
        session.Rate(Rating.Again);
        session.Rate(Rating.Easy);
        session.Rate(Rating.Good);
        session.Rate(Rating.Hard);
    }

    [Fact]
    public void Export_then_overwrite_restore_reproduces_everything()
    {
        SeedSource();
        var json = ServiceFor(_source).Export();

        ServiceFor(_target).Restore(json, RestoreMode.Overwrite);

        Assert.Equal(Normalized(_source.Store.ExportAll()), Normalized(_target.Store.ExportAll()));
        Assert.Equal(new DailyLimits(7, 70), _target.Settings.LoadLimits());
        Assert.Equal(_source.Stats.Get(), _target.Stats.Get(), new StatsComparer());
        Assert.Equal(
            _source.Decks.GetOverviews().Select(o => (o.Deck.Name, o.TotalCards, o.DueToday, o.NewToday)),
            _target.Decks.GetOverviews().Select(o => (o.Deck.Name, o.TotalCards, o.DueToday, o.NewToday)));
    }

    [Fact]
    public void Overwrite_removes_existing_data_not_in_backup()
    {
        SeedSource();
        _target.DeckWithCsv("本機牌組", "x,y\n");

        ServiceFor(_target).Restore(ServiceFor(_source).Export(), RestoreMode.Overwrite);

        Assert.DoesNotContain(_target.Decks.GetOverviews(), o => o.Deck.Name == "本機牌組");
        Assert.Equal(3, _target.Store.GetAllCards().Count);
    }

    [Fact]
    public void Merge_keeps_local_data_and_adds_backup_data()
    {
        SeedSource();
        _target.Settings.SaveLimits(new DailyLimits(3, 30));
        _target.DeckWithCsv("本機牌組", "x,y\n");

        ServiceFor(_target).Restore(ServiceFor(_source).Export(), RestoreMode.Merge);

        var names = _target.Decks.GetOverviews().Select(o => o.Deck.Name).ToList();
        Assert.Contains("本機牌組", names);
        Assert.Contains("N2", names);
        Assert.Equal(4, _target.Store.GetAllCards().Count);
        Assert.Equal(4, _target.Store.ExportAll().ReviewLogs.Count);
        Assert.Equal(new DailyLimits(3, 30), _target.Settings.LoadLimits()); // 合併不覆寫本機設定
    }

    [Fact]
    public void Merge_is_idempotent()
    {
        SeedSource();
        var json = ServiceFor(_source).Export();
        ServiceFor(_target).Restore(json, RestoreMode.Merge);
        var once = Normalized(_target.Store.ExportAll());
        ServiceFor(_target).Restore(json, RestoreMode.Merge);
        Assert.Equal(once, Normalized(_target.Store.ExportAll()));
    }

    [Fact]
    public void Sample_backup_file_restores()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "backup_example.json"));
        ServiceFor(_target).Restore(json, RestoreMode.Overwrite);

        var overviews = _target.Decks.GetOverviews();
        Assert.Equal(["N2 範例", "空的牌組"], overviews.Select(o => o.Deck.Name));
        Assert.Equal(5, overviews[0].TotalCards);
        Assert.NotEmpty(_target.Store.GetReviewDates());
    }

    [Fact]
    public void Invalid_json_is_rejected_and_database_untouched()
    {
        _target.DeckWithCsv("本機牌組", "x,y\n");
        var before = Normalized(_target.Store.ExportAll());

        Assert.Throws<InvalidDataException>(() => ServiceFor(_target).Restore("{ not json", RestoreMode.Overwrite));
        Assert.Throws<InvalidDataException>(() => ServiceFor(_target).Restore("""{"version":99}""", RestoreMode.Overwrite));

        Assert.Equal(before, Normalized(_target.Store.ExportAll()));
    }

    private sealed class StatsComparer : IEqualityComparer<Core.Services.StatsSnapshot>
    {
        public bool Equals(Core.Services.StatsSnapshot? x, Core.Services.StatsSnapshot? y) =>
            x is not null && y is not null
            && x.Today == y.Today && x.CardsReviewedToday == y.CardsReviewedToday
            && x.RatingsToday == y.RatingsToday && x.Streak == y.Streak
            && x.Forecast.SequenceEqual(y.Forecast);

        public int GetHashCode(Core.Services.StatsSnapshot obj) => obj.Today.GetHashCode();
    }
}

public class BackupMergerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid DeckA = Guid.NewGuid();

    private static BackupDeck Deck(Guid guid, string name, int updatedDay = 0) =>
        new(guid, name, T0, T0.AddDays(updatedDay));

    private static BackupCard Card(Guid guid, string front, int updatedDay, int interval = 1, Guid? deck = null) =>
        new(guid, deck ?? DeckA, front, "back", [], 2.5, 1, interval, new DateOnly(2026, 10, 2), T0, T0, T0.AddDays(updatedDay));

    private static BackupDocument Doc(BackupDeck[] decks, BackupCard[] cards, BackupReviewLog[]? logs = null) =>
        new() { Decks = decks, Cards = cards, ReviewLogs = logs ?? [] };

    [Fact]
    public void Later_updated_card_wins_in_both_directions()
    {
        var newerInBackup = Guid.NewGuid();
        var newerLocally = Guid.NewGuid();
        var current = Doc([Deck(DeckA, "A")], [Card(newerInBackup, "x", 1, interval: 1), Card(newerLocally, "y", 5, interval: 50)]);
        var incoming = Doc([Deck(DeckA, "A")], [Card(newerInBackup, "x", 3, interval: 30), Card(newerLocally, "y", 2, interval: 20)]);

        var merged = BackupMerger.Merge(current, incoming);

        Assert.Equal(30, merged.Cards.Single(c => c.Guid == newerInBackup).Interval);
        Assert.Equal(50, merged.Cards.Single(c => c.Guid == newerLocally).Interval);
    }

    [Fact]
    public void Later_renamed_deck_wins()
    {
        var current = Doc([Deck(DeckA, "舊名", updatedDay: 1)], []);
        var incoming = Doc([Deck(DeckA, "新名", updatedDay: 2)], []);
        Assert.Equal("新名", BackupMerger.Merge(current, incoming).Decks.Single().Name);
        Assert.Equal("新名", BackupMerger.Merge(incoming, current).Decks.Single().Name);
    }

    [Fact]
    public void Different_decks_with_same_name_are_both_kept_with_suffix()
    {
        var current = Doc([Deck(DeckA, "N2")], []);
        var incoming = Doc([Deck(Guid.NewGuid(), "n2")], []);
        var names = BackupMerger.Merge(current, incoming).Decks.Select(d => d.Name).ToList();
        Assert.Equal(["N2", "n2 (2)"], names);
    }

    [Fact]
    public void Same_front_in_same_deck_with_different_guid_keeps_later_and_drops_its_logs()
    {
        var local = Guid.NewGuid();
        var remote = Guid.NewGuid();
        var remoteLog = new BackupReviewLog(Guid.NewGuid(), remote, T0, new DateOnly(2026, 10, 1), 3, true);
        var current = Doc([Deck(DeckA, "A")], [Card(local, "Apple", 5)]);
        var incoming = Doc([Deck(DeckA, "A")], [Card(remote, " apple", 1)], [remoteLog]);

        var merged = BackupMerger.Merge(current, incoming);

        Assert.Equal(local, merged.Cards.Single().Guid);
        Assert.Empty(merged.ReviewLogs);
    }

    [Fact]
    public void Review_logs_are_unioned_by_guid()
    {
        var card = Guid.NewGuid();
        var shared = new BackupReviewLog(Guid.NewGuid(), card, T0, new DateOnly(2026, 10, 1), 3, true);
        var onlyRemote = new BackupReviewLog(Guid.NewGuid(), card, T0.AddDays(1), new DateOnly(2026, 10, 2), 4, false);
        var current = Doc([Deck(DeckA, "A")], [Card(card, "x", 0)], [shared]);
        var incoming = Doc([Deck(DeckA, "A")], [Card(card, "x", 0)], [shared, onlyRemote]);

        Assert.Equal(2, BackupMerger.Merge(current, incoming).ReviewLogs.Count);
    }

    [Fact]
    public void Validator_rejects_dangling_references_and_duplicates()
    {
        var orphanCard = Doc([], [Card(Guid.NewGuid(), "x", 0)]);
        Assert.Contains("牌組", Assert.Throws<InvalidDataException>(() => BackupValidator.Validate(orphanCard)).Message);

        var orphanLog = Doc([Deck(DeckA, "A")], [],
            [new BackupReviewLog(Guid.NewGuid(), Guid.NewGuid(), T0, new DateOnly(2026, 10, 1), 3, true)]);
        Assert.Throws<InvalidDataException>(() => BackupValidator.Validate(orphanLog));

        var dupFront = Doc([Deck(DeckA, "A")], [Card(Guid.NewGuid(), "x", 0), Card(Guid.NewGuid(), "X ", 0)]);
        Assert.Throws<InvalidDataException>(() => BackupValidator.Validate(dupFront));

        var badRating = Doc([Deck(DeckA, "A")], [Card(DeckA, "x", 0)],
            [new BackupReviewLog(Guid.NewGuid(), DeckA, T0, new DateOnly(2026, 10, 1), 9, true)]);
        Assert.Throws<InvalidDataException>(() => BackupValidator.Validate(badRating));
    }

    [Fact]
    public void Serializer_roundtrips_document()
    {
        var doc = Doc([Deck(DeckA, "A")], [Card(Guid.NewGuid(), "前\n面,\"x\"", 0)]) with
        {
            ExportedAt = T0,
            Settings = new BackupSettings(20, 100),
        };
        var json = BackupSerializer.Serialize(doc);
        Assert.Equal(json, BackupSerializer.Serialize(BackupSerializer.Deserialize(json)));
        Assert.Contains("\"dueDate\": \"2026-10-02\"", json);
    }
}
