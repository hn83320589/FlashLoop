using System.Text;
using FlashLoop.App.Tests.Support;
using FlashLoop.Core.Backup;
using FlashLoop.Core.Model;

namespace FlashLoop.App.Tests;

/// <summary>驗收 #12：以真實的資料庫檔案與備份資料夾驗證匯出、覆蓋、合併與錯誤處理。</summary>
public class BackupFlowTests
{
    private static string SeededBackup(AppHarness source)
    {
        var samples = Path.Combine(AppContext.BaseDirectory, "samples");
        var deck = source.Services.Decks.Create("N2 基礎");
        source.Services.Import.Import(deck.Id, File.ReadAllBytes(Path.Combine(samples, "n2_basic.csv")));
        source.Settings.SaveLimits(new DailyLimits(5, 15));
        source.ReviewAll("N2 基礎", Keys.D3);
        return source.Services.Backup.Export();
    }

    [Fact]
    public void Overwrite_reproduces_the_source_database_and_archives_what_it_replaced() => Sta.Run(() =>
    {
        using var source = new AppHarness();
        using var target = new AppHarness();
        var json = SeededBackup(source);
        var local = target.Services.Decks.Create("會被覆蓋的本機牌組");
        target.Services.Import.Import(local.Id, Encoding.UTF8.GetBytes("本機卡,本機背面\n"));
        var localDump = target.Dump();

        var archived = target.Services.Backup.Restore(json, RestoreMode.Overwrite);

        Assert.Equal(source.Dump(), target.Dump());
        Assert.Equal(new DailyLimits(5, 15), target.Settings.LoadLimits());

        Assert.NotNull(archived);
        Assert.Equal(target.BackupsDirectory, Path.GetDirectoryName(archived));
        target.Services.Backup.Restore(File.ReadAllText(archived), RestoreMode.Overwrite);
        Assert.Equal(localDump, target.Dump());
    });

    [Fact]
    public void Merge_keeps_local_data_and_settings_and_is_idempotent() => Sta.Run(() =>
    {
        using var source = new AppHarness();
        using var target = new AppHarness();
        var json = SeededBackup(source);
        var local = target.Services.Decks.Create("本機牌組");
        target.Services.Import.Import(local.Id, Encoding.UTF8.GetBytes("本機卡,本機背面\n"));

        Assert.Null(target.Services.Backup.Restore(json, RestoreMode.Merge));

        // 備份帶入今天評過的 5 張新卡紀錄，因此全域新卡額度剩 20 - 5 = 15
        Assert.Equal(new[] { "N2 基礎 30/0/15", "本機牌組 1/0/1" }.Order(StringComparer.Ordinal),
            target.DeckRows().Order(StringComparer.Ordinal));
        Assert.Equal(DailyLimits.Default, target.Settings.LoadLimits());
        Assert.False(Directory.Exists(target.BackupsDirectory));
        var once = target.Dump();
        target.Services.Backup.Restore(json, RestoreMode.Merge);
        Assert.Equal(once, target.Dump());
    });

    [Fact]
    public void Corrupt_backup_is_rejected_and_nothing_changes() => Sta.Run(app =>
    {
        SeededBackup(app);
        var before = app.Dump();

        Assert.Throws<InvalidDataException>(() => app.Services.Backup.Restore("{ \"version\": 1, \"decks\": [", RestoreMode.Overwrite));

        Assert.Equal(before, app.Dump());
        Assert.False(Directory.Exists(app.BackupsDirectory));
    });

    [Fact]
    public void Sample_backup_file_restores() => Sta.Run(app =>
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "backup_example.json"));

        app.Services.Backup.Restore(json, RestoreMode.Overwrite);

        Assert.Equal(["N2 範例 5", "空的牌組 0"], app.Services.Decks.GetOverviews().Select(o => $"{o.Deck.Name} {o.TotalCards}"));
    });
}
