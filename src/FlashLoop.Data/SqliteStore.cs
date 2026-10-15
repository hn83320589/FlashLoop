using System.Globalization;
using FlashLoop.Core.Backup;
using FlashLoop.Core.Csv;
using FlashLoop.Core.Model;
using FlashLoop.Core.Persistence;
using FlashLoop.Core.Review;
using FlashLoop.Core.Scheduling;
using Microsoft.Data.Sqlite;

namespace FlashLoop.Data;

/// <summary>
/// SQLite 實作。每個操作開啟一條連線（Microsoft.Data.Sqlite 預設有連線池），
/// 有多個寫入的操作一律包在 transaction 中。
/// </summary>
public sealed class SqliteStore : IDeckRepository, ICardRepository, IReviewLogRepository, IReviewStore, IBackupStore
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string CardColumns =
        "id, guid, deck_id, front, back, tags, ease, repetitions, interval_days, due_date, last_reviewed_at, created_at, updated_at";

    private readonly string _connectionString;

    public SqliteStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();
        EnsureSchema();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureSchema()
    {
        using var c = Open();
        Execute(c, """
            CREATE TABLE IF NOT EXISTS decks (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                guid        TEXT NOT NULL UNIQUE,
                name        TEXT NOT NULL UNIQUE COLLATE NOCASE,
                created_at  TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS cards (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                guid              TEXT NOT NULL UNIQUE,
                deck_id           INTEGER NOT NULL REFERENCES decks(id) ON DELETE CASCADE,
                front             TEXT NOT NULL,
                back              TEXT NOT NULL,
                tags              TEXT NOT NULL DEFAULT '',
                front_key         TEXT NOT NULL,
                ease              REAL NOT NULL,
                repetitions       INTEGER NOT NULL,
                interval_days     INTEGER NOT NULL,
                due_date          TEXT NULL,
                last_reviewed_at  TEXT NULL,
                created_at        TEXT NOT NULL,
                updated_at        TEXT NOT NULL,
                UNIQUE (deck_id, front_key)
            );
            CREATE INDEX IF NOT EXISTS ix_cards_due_date ON cards(due_date);
            CREATE TABLE IF NOT EXISTS review_logs (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                guid         TEXT NOT NULL UNIQUE,
                card_id      INTEGER NOT NULL REFERENCES cards(id) ON DELETE CASCADE,
                reviewed_at  TEXT NOT NULL,
                review_date  TEXT NOT NULL,
                rating       INTEGER NOT NULL CHECK (rating BETWEEN 1 AND 4),
                was_new      INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_review_logs_date ON review_logs(review_date);
            PRAGMA user_version = 1;
            """);
    }

    // ---------- Decks ----------

    public IReadOnlyList<Deck> GetAllDecks()
    {
        using var c = Open();
        return Query(c, "SELECT id, guid, name, created_at, updated_at FROM decks ORDER BY name COLLATE NOCASE, id", ReadDeck);
    }

    public Deck? FindDeck(long id)
    {
        using var c = Open();
        return Query(c, "SELECT id, guid, name, created_at, updated_at FROM decks WHERE id = $id", ReadDeck, ("$id", id))
            .SingleOrDefault();
    }

    public bool DeckNameExists(string name, long? excludingId = null)
    {
        using var c = Open();
        var count = Scalar<long>(c, "SELECT COUNT(*) FROM decks WHERE name = $name COLLATE NOCASE AND id <> $id",
            ("$name", name.Trim()), ("$id", excludingId ?? -1));
        return count > 0;
    }

    public Deck AddDeck(string name, DateTimeOffset now)
    {
        using var c = Open();
        var guid = Guid.NewGuid();
        Execute(c, "INSERT INTO decks (guid, name, created_at, updated_at) VALUES ($guid, $name, $now, $now)",
            ("$guid", guid.ToString()), ("$name", name), ("$now", Ts(now)));
        var id = Scalar<long>(c, "SELECT last_insert_rowid()");
        return new Deck(id, guid, name, now, now);
    }

    public void RenameDeck(long id, string name, DateTimeOffset now)
    {
        using var c = Open();
        var affected = Execute(c, "UPDATE decks SET name = $name, updated_at = $now WHERE id = $id",
            ("$name", name), ("$now", Ts(now)), ("$id", id));
        EnsureFound(affected, "牌組", id);
    }

    public void DeleteDeck(long id)
    {
        using var c = Open();
        var affected = Execute(c, "DELETE FROM decks WHERE id = $id", ("$id", id));
        EnsureFound(affected, "牌組", id);
    }

    // ---------- Cards ----------

    public IReadOnlyList<Card> GetCardsByDeck(long deckId)
    {
        using var c = Open();
        return Query(c, $"SELECT {CardColumns} FROM cards WHERE deck_id = $deck ORDER BY id", ReadCard, ("$deck", deckId));
    }

    public IReadOnlyList<Card> GetAllCards()
    {
        using var c = Open();
        return Query(c, $"SELECT {CardColumns} FROM cards ORDER BY id", ReadCard);
    }

    public IReadOnlySet<string> GetFrontKeys(long deckId)
    {
        using var c = Open();
        return Query(c, "SELECT front_key FROM cards WHERE deck_id = $deck", r => r.GetString(0), ("$deck", deckId))
            .ToHashSet(StringComparer.Ordinal);
    }

    public void AddCards(long deckId, IEnumerable<ImportedCard> cards, DateTimeOffset now)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var card in cards)
        {
            InsertCard(c, deckId, new Card
            {
                Guid = Guid.NewGuid(),
                DeckId = deckId,
                Front = card.Front,
                Back = card.Back,
                Tags = card.Tags,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        tx.Commit();
    }

    // ---------- Review logs ----------

    public IReadOnlyList<ReviewLog> GetLogsOn(DateOnly day)
    {
        using var c = Open();
        return Query(c,
            "SELECT id, guid, card_id, reviewed_at, review_date, rating, was_new FROM review_logs WHERE review_date = $d ORDER BY id",
            ReadLog, ("$d", Day(day)));
    }

    public IReadOnlyList<DateOnly> GetReviewDates()
    {
        using var c = Open();
        return Query(c, "SELECT DISTINCT review_date FROM review_logs ORDER BY review_date", r => ParseDay(r.GetString(0)));
    }

    // ---------- IReviewStore ----------

    public ReviewLog SaveReview(Card updated, ReviewLog log)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        var affected = UpdateCardSchedule(c, updated);
        EnsureFound(affected, "卡片", updated.Id);
        var id = InsertLog(c, log, log.CardId);
        tx.Commit();
        return log with { Id = id };
    }

    public void UndoReview(Card previous, long logId)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        EnsureFound(UpdateCardSchedule(c, previous), "卡片", previous.Id);
        EnsureFound(Execute(c, "DELETE FROM review_logs WHERE id = $id", ("$id", logId)), "評分紀錄", logId);
        tx.Commit();
    }

    // ---------- IBackupStore ----------

    public BackupDocument ExportAll()
    {
        using var c = Open();
        var decks = Query(c, "SELECT id, guid, name, created_at, updated_at FROM decks ORDER BY id", ReadDeck);
        var cards = Query(c, $"SELECT {CardColumns} FROM cards ORDER BY id", ReadCard);
        var logs = Query(c, "SELECT id, guid, card_id, reviewed_at, review_date, rating, was_new FROM review_logs ORDER BY id", ReadLog);

        var deckGuids = decks.ToDictionary(d => d.Id, d => d.Guid);
        var cardGuids = cards.ToDictionary(x => x.Id, x => x.Guid);

        return new BackupDocument
        {
            Decks = decks.Select(d => new BackupDeck(d.Guid, d.Name, d.CreatedAt, d.UpdatedAt)).ToList(),
            Cards = cards.Select(x => new BackupCard(
                x.Guid, deckGuids[x.DeckId], x.Front, x.Back, x.Tags,
                x.Schedule.Ease, x.Schedule.Repetitions, x.Schedule.Interval, x.Schedule.DueDate,
                x.LastReviewedAt, x.CreatedAt, x.UpdatedAt)).ToList(),
            ReviewLogs = logs.Select(l => new BackupReviewLog(
                l.Guid, cardGuids[l.CardId], l.ReviewedAt, l.ReviewDate, (int)l.Rating, l.WasNew)).ToList(),
        };
    }

    public void ReplaceAll(BackupDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var c = Open();
        using var tx = c.BeginTransaction();
        Execute(c, "DELETE FROM review_logs; DELETE FROM cards; DELETE FROM decks;");

        var deckIds = new Dictionary<Guid, long>();
        foreach (var d in document.Decks)
        {
            Execute(c, "INSERT INTO decks (guid, name, created_at, updated_at) VALUES ($guid, $name, $created, $updated)",
                ("$guid", d.Guid.ToString()), ("$name", d.Name), ("$created", Ts(d.CreatedAt)), ("$updated", Ts(d.UpdatedAt)));
            deckIds[d.Guid] = Scalar<long>(c, "SELECT last_insert_rowid()");
        }

        var cardIds = new Dictionary<Guid, long>();
        foreach (var x in document.Cards)
        {
            var deckId = deckIds[x.DeckGuid];
            cardIds[x.Guid] = InsertCard(c, deckId, new Card
            {
                Guid = x.Guid,
                DeckId = deckId,
                Front = x.Front,
                Back = x.Back,
                Tags = x.Tags,
                Schedule = new CardSchedule(x.Ease, x.Repetitions, x.Interval, x.DueDate),
                LastReviewedAt = x.LastReviewedAt,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
            });
        }

        foreach (var l in document.ReviewLogs)
        {
            var cardId = cardIds[l.CardGuid];
            InsertLog(c, new ReviewLog(0, l.Guid, cardId, l.ReviewedAt, l.ReviewDate, (Rating)l.Rating, l.WasNew), cardId);
        }

        tx.Commit();
    }

    // ---------- helpers ----------

    private static long InsertCard(SqliteConnection c, long deckId, Card card)
    {
        Execute(c, """
            INSERT INTO cards (guid, deck_id, front, back, tags, front_key, ease, repetitions, interval_days,
                               due_date, last_reviewed_at, created_at, updated_at)
            VALUES ($guid, $deck, $front, $back, $tags, $key, $ease, $reps, $interval, $due, $last, $created, $updated)
            """,
            ("$guid", card.Guid.ToString()), ("$deck", deckId), ("$front", card.Front), ("$back", card.Back),
            ("$tags", CardText.JoinTags(card.Tags)), ("$key", CardText.FrontKey(card.Front)),
            ("$ease", card.Schedule.Ease), ("$reps", card.Schedule.Repetitions), ("$interval", card.Schedule.Interval),
            ("$due", card.Schedule.DueDate is { } due ? Day(due) : null),
            ("$last", card.LastReviewedAt is { } last ? Ts(last) : null),
            ("$created", Ts(card.CreatedAt)), ("$updated", Ts(card.UpdatedAt)));
        return Scalar<long>(c, "SELECT last_insert_rowid()");
    }

    private static int UpdateCardSchedule(SqliteConnection c, Card card) => Execute(c, """
        UPDATE cards SET ease = $ease, repetitions = $reps, interval_days = $interval, due_date = $due,
                         last_reviewed_at = $last, updated_at = $updated
        WHERE id = $id
        """,
        ("$ease", card.Schedule.Ease), ("$reps", card.Schedule.Repetitions), ("$interval", card.Schedule.Interval),
        ("$due", card.Schedule.DueDate is { } due ? Day(due) : null),
        ("$last", card.LastReviewedAt is { } last ? Ts(last) : null),
        ("$updated", Ts(card.UpdatedAt)), ("$id", card.Id));

    private static long InsertLog(SqliteConnection c, ReviewLog log, long cardId)
    {
        Execute(c, """
            INSERT INTO review_logs (guid, card_id, reviewed_at, review_date, rating, was_new)
            VALUES ($guid, $card, $at, $date, $rating, $new)
            """,
            ("$guid", log.Guid.ToString()), ("$card", cardId), ("$at", Ts(log.ReviewedAt)),
            ("$date", Day(log.ReviewDate)), ("$rating", (int)log.Rating), ("$new", log.WasNew ? 1 : 0));
        return Scalar<long>(c, "SELECT last_insert_rowid()");
    }

    private static Deck ReadDeck(SqliteDataReader r) => new(
        r.GetInt64(0), Guid.Parse(r.GetString(1)), r.GetString(2), ParseTs(r.GetString(3)), ParseTs(r.GetString(4)));

    private static Card ReadCard(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Guid = Guid.Parse(r.GetString(1)),
        DeckId = r.GetInt64(2),
        Front = r.GetString(3),
        Back = r.GetString(4),
        Tags = CardText.ParseTags(r.GetString(5)),
        Schedule = new CardSchedule(
            r.GetDouble(6), r.GetInt32(7), r.GetInt32(8), r.IsDBNull(9) ? null : ParseDay(r.GetString(9))),
        LastReviewedAt = r.IsDBNull(10) ? null : ParseTs(r.GetString(10)),
        CreatedAt = ParseTs(r.GetString(11)),
        UpdatedAt = ParseTs(r.GetString(12)),
    };

    private static ReviewLog ReadLog(SqliteDataReader r) => new(
        r.GetInt64(0), Guid.Parse(r.GetString(1)), r.GetInt64(2), ParseTs(r.GetString(3)),
        ParseDay(r.GetString(4)), (Rating)r.GetInt32(5), r.GetInt32(6) != 0);

    private static string Ts(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTs(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string Day(DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static DateOnly ParseDay(string value) => DateOnly.ParseExact(value, DateFormat, CultureInfo.InvariantCulture);

    private static void EnsureFound(int affected, string what, long id)
    {
        if (affected == 0)
        {
            throw new KeyNotFoundException($"找不到{what}（Id = {id}）");
        }
    }

    private static SqliteCommand Command(SqliteConnection c, string sql, (string Name, object? Value)[] parameters)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        return cmd;
    }

    private static int Execute(SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(c, sql, parameters);
        return cmd.ExecuteNonQuery();
    }

    private static T Scalar<T>(SqliteConnection c, string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(c, sql, parameters);
        return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T), CultureInfo.InvariantCulture);
    }

    private static List<T> Query<T>(SqliteConnection c, string sql, Func<SqliteDataReader, T> map,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(c, sql, parameters);
        using var reader = cmd.ExecuteReader();
        var result = new List<T>();
        while (reader.Read())
        {
            result.Add(map(reader));
        }
        return result;
    }
}
