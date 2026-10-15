using FlashLoop.Core.Model;
using FlashLoop.Data;
using Microsoft.Data.Sqlite;

namespace FlashLoop.Tests.Data;

/// <summary>啟動時設定檔或資料庫損毀的復原：先證明損毀會被偵測，再證明移開後可重新開始且原檔被保留。</summary>
public sealed class DataFileRecoveryTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 15, 9, 30, 5, TimeSpan.FromHours(8));
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlashLoopTests", Guid.NewGuid().ToString("N"));

    public DataFileRecoveryTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Corrupt_settings_file_is_moved_aside_and_defaults_can_be_saved()
    {
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "{ broken");
        var settings = new JsonSettingsStore(path);
        Assert.Throws<InvalidDataException>(settings.LoadLimits);

        var backup = DataFileRecovery.MoveAside(path, At);
        settings.SaveLimits(DailyLimits.Default);

        Assert.Equal(Path.Combine(_directory, "settings.json.20261015-093005.bak"), backup);
        Assert.Equal("{ broken", File.ReadAllText(backup));
        Assert.Equal(DailyLimits.Default, settings.LoadLimits());
    }

    [Fact]
    public void Corrupt_database_is_moved_aside_with_its_journal_and_a_new_one_opens()
    {
        var path = Path.Combine(_directory, "flashloop.db");
        File.WriteAllText(path, "this is not a database");
        Assert.Throws<SqliteException>(() => new SqliteStore(path));
        SqliteConnection.ClearAllPools();
        // 開啟失敗後仍留在原地的 journal（SQLite 只會自行刪除格式無效的 journal，有效的會被套用到新資料庫）
        File.WriteAllText(path + "-journal", "stale journal");

        var backup = DataFileRecovery.MoveAside(path, At);
        var store = new SqliteStore(path);

        Assert.Equal("this is not a database", File.ReadAllText(backup));
        Assert.Equal("stale journal", File.ReadAllText(backup + "-journal"));
        Assert.Empty(store.GetAllDecks());
    }

    [Fact]
    public void Never_overwrites_an_earlier_backup()
    {
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(path, "first");
        var first = DataFileRecovery.MoveAside(path, At);
        File.WriteAllText(path, "second");
        var second = DataFileRecovery.MoveAside(path, At);

        Assert.NotEqual(first, second);
        Assert.Equal("first", File.ReadAllText(first));
        Assert.Equal("second", File.ReadAllText(second));
        Assert.False(File.Exists(path));
    }
}
