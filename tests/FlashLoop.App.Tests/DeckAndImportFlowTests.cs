using System.Reflection;
using FlashLoop.App.Tests.Support;
using FlashLoop.Core.Csv;
using FlashLoop.Core.Services;

namespace FlashLoop.App.Tests;

/// <summary>驗收 #1（牌組管理、重開後不變）、#2 與 #3（CSV 匯入與摘要）的端到端流程。</summary>
public class DeckAndImportFlowTests
{
    private static byte[] Sample(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", name));

    [Fact]
    public void Empty_data_directory_shows_empty_state_hint() => Sta.Run(app =>
    {
        Assert.False(app.DeckGrid.Visible);
        Assert.True(AppHarness.Field<Label>(app.DeckList, "_empty").Visible);
    });

    [Fact]
    public void Create_rename_delete_and_counts_survive_restart() => Sta.Run(app =>
    {
        var decks = app.Services.Decks;
        var n2 = decks.Create("N2 基礎");
        var temp = decks.Create("暫存");
        decks.Rename(temp.Id, "會話");
        var doomed = decks.Create("刪除測試");
        app.Services.Import.Import(n2.Id, Sample("n2_basic.csv"));
        app.Services.Import.Import(temp.Id, Sample("special_bom.csv"));
        app.Services.Import.Import(doomed.Id, Sample("with_errors.csv"));

        Assert.Contains("已有名為", Assert.Throws<ValidationException>(() => decks.Create("n2 基礎")).Message);
        Assert.Contains("空白", Assert.Throws<ValidationException>(() => decks.Rename(n2.Id, "   ")).Message);

        decks.Delete(doomed.Id);
        var before = app.DeckRows();
        Assert.Equal(["N2 基礎 30/0/20", "會話 6/0/6"], before);
        Assert.Equal(0, app.Scalar($"SELECT COUNT(*) FROM cards WHERE deck_id = {doomed.Id}"));

        app.Restart();

        Assert.Equal(before, app.DeckRows());
    });

    [Fact]
    public void Special_character_sample_keeps_every_field_intact() => Sta.Run(app =>
    {
        var deck = app.Services.Decks.Create("特殊字元");
        var summary = app.Services.Import.Import(deck.Id, Sample("special_bom.csv"));

        Assert.Equal((6, 0, 0), (summary.Added, summary.Skipped, summary.Errors.Count));
        var cards = app.Store.GetCardsByDeck(deck.Id).ToDictionary(c => c.Front);
        Assert.Contains("お願い, します", cards.Keys);
        Assert.Contains("彼は\"天才\"だ", cards.Keys);
        Assert.Equal("きをつける\n1. 小心\n2. 注意（背面含換行）", cards["気をつける"].Back);
        Assert.Contains("😀", cards.Keys);
        Assert.DoesNotContain(cards.Keys, k => k.StartsWith('﻿'));
        Assert.Equal(["標籤", "也會", "整理"], cards["前後空白"].Tags);
    });

    [Fact]
    public void Error_sample_summary_reports_counts_line_numbers_and_reasons() => Sta.Run(app =>
    {
        var deck = app.Services.Decks.Create("錯誤範例");
        var summary = app.Services.Import.Import(deck.Id, Sample("with_errors.csv"));

        Assert.Equal((4, 2, 6), (summary.Added, summary.Skipped, summary.Errors.Count));
        Assert.Equal([4, 6], summary.SkippedRows.Select(s => s.LineNumber));
        Assert.Equal([7, 8, 9, 10, 11, 14], summary.Errors.Select(e => e.LineNumber));
        Assert.All(summary.SkippedRows.Concat(summary.Errors), issue => Assert.False(string.IsNullOrWhiteSpace(issue.Reason)));

        var text = ShowImportResultDialog(summary);
        Assert.Contains("新增 4 張", text);
        Assert.Contains("略過 2 張", text);
        Assert.Contains("錯誤 6 列", text);
        foreach (var issue in summary.SkippedRows.Concat(summary.Errors))
        {
            Assert.Contains(issue.Reason, text);
        }
    });

    [Fact]
    public void Importing_the_same_file_again_skips_every_row() => Sta.Run(app =>
    {
        var deck = app.Services.Decks.Create("N2 基礎");
        app.Services.Import.Import(deck.Id, Sample("n2_basic.csv"));

        var again = app.Services.Import.Import(deck.Id, Sample("n2_basic.csv"));

        Assert.Equal((0, 30, 0), (again.Added, again.Skipped, again.Errors.Count));
        Assert.Contains("已存在於此牌組", again.SkippedRows[0].Reason);
    });

    private static string ShowImportResultDialog(ImportSummary summary)
    {
        var type = typeof(MainForm).Assembly.GetType("FlashLoop.App.Dialogs.ImportResultDialog")!;
        using var dialog = (Form)type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0].Invoke(["with_errors.csv", summary]);
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(-32000, -32000);
        dialog.ShowInTaskbar = false;
        dialog.CreateControl();
        AppHarness.Pump();
        return AppHarness.AllText(dialog);
    }
}
