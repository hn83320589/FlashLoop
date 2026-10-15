using FlashLoop.Core.Csv;

namespace FlashLoop.Tests.Csv;

/// <summary>驗證 samples/ 內的範例檔，確保 README 描述的匯入結果與實際一致。</summary>
public class SampleFileTests
{
    private static readonly IReadOnlySet<string> NoExisting = new HashSet<string>();

    private static byte[] Sample(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", name));

    [Fact]
    public void Basic_word_list_imports_all_30_rows()
    {
        var result = CsvImporter.Analyze(Sample("n2_basic.csv"), NoExisting);
        Assert.Equal(30, result.Summary.Added);
        Assert.Equal(0, result.Summary.Skipped);
        Assert.Empty(result.Summary.Errors);
        Assert.Equal(["N2", "形容動詞"], result.NewCards[0].Tags);
    }

    [Fact]
    public void Error_sample_reports_expected_counts_and_lines()
    {
        var bytes = Sample("with_errors.csv");
        Assert.False(bytes.AsSpan().StartsWith((byte[])[0xEF, 0xBB, 0xBF]));

        var summary = CsvImporter.Analyze(bytes, NoExisting).Summary;

        Assert.Equal(4, summary.Added);
        Assert.Equal([4, 6], summary.SkippedRows.Select(s => s.LineNumber));
        Assert.Equal([7, 8, 9, 10, 11, 14], summary.Errors.Select(e => e.LineNumber));
    }

    [Fact]
    public void Special_character_sample_with_bom_and_crlf_is_parsed_correctly()
    {
        var bytes = Sample("special_bom.csv");
        Assert.True(bytes.AsSpan().StartsWith((byte[])[0xEF, 0xBB, 0xBF]));

        var result = CsvImporter.Analyze(bytes, NoExisting);

        Assert.Equal(6, result.Summary.Added);
        Assert.Empty(result.Summary.Errors);
        var cards = result.NewCards;
        Assert.Equal("お願い, します", cards[0].Front);
        Assert.Equal("彼は\"天才\"だ", cards[1].Front);
        Assert.Equal("きをつける\n1. 小心\n2. 注意（背面含換行）", cards[2].Back);
        Assert.Equal(7, cards[3].LineNumber); // 「気をつける」的背面佔第 4–6 行，café 在第 7 行
        Assert.Equal("😀", cards[4].Front);
        Assert.Equal("前後空白", cards[5].Front);
        Assert.Equal(["標籤", "也會", "整理"], cards[5].Tags);
    }
}
