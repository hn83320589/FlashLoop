using FlashLoop.Core.Model;
using FlashLoop.Core.Persistence;
using FlashLoop.Core.Services;
using FlashLoop.Data;
using Microsoft.Data.Sqlite;

namespace FlashLoop.Tests.Support;

/// <summary>每個測試一個暫存 SQLite 檔，結束時刪除。</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlashLoopTests", Guid.NewGuid().ToString("N"));

    public TestDatabase(FakeTimeProvider? time = null)
    {
        Directory.CreateDirectory(_directory);
        Time = time ?? new FakeTimeProvider(2026, 10, 15);
        Settings = new JsonSettingsStore(Path.Combine(_directory, "settings.json"));
        Store = new SqliteStore(DatabasePath);
    }

    public string DatabasePath => Path.Combine(_directory, "flashloop.db");
    public FakeTimeProvider Time { get; }
    public ISettingsStore Settings { get; }
    public SqliteStore Store { get; private set; }

    /// <summary>模擬關閉程式後重新開啟。</summary>
    public void Reopen()
    {
        SqliteConnection.ClearAllPools();
        Store = new SqliteStore(DatabasePath);
    }

    public DeckService Decks => new(Store, Store, Store, Settings, Time);
    public ImportService Import => new(Store, Time);
    public ReviewService Review => new(Store, Store, Store, Settings, Time);
    public StatsService Stats => new(Store, Store, Time);

    public SqliteConnection OpenRaw()
    {
        var c = new SqliteConnection($"Data Source={DatabasePath}");
        c.Open();
        return c;
    }

    public Deck DeckWithCsv(string name, string csv)
    {
        var deck = Decks.Create(name);
        Import.Import(deck.Id, System.Text.Encoding.UTF8.GetBytes(csv));
        return deck;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // 暫存目錄刪除失敗不影響測試結果；留給系統清理
        }
    }
}
