using FlashLoop.Core.Csv;

namespace FlashLoop.Tests.Csv;

public class CsvImporterTests
{
    private static readonly IReadOnlySet<string> NoExisting = new HashSet<string>();

    [Fact]
    public void Imports_valid_rows_with_tags()
    {
        var result = CsvImporter.Analyze("勉強,べんきょう,N2;動詞\n試験,しけん,\n", NoExisting);

        Assert.Equal(2, result.Summary.Added);
        Assert.Equal(0, result.Summary.Skipped);
        Assert.Empty(result.Summary.Errors);
        Assert.Equal(["N2", "動詞"], result.NewCards[0].Tags);
        Assert.Empty(result.NewCards[1].Tags);
    }

    [Fact]
    public void Two_columns_without_tags_is_valid()
    {
        var result = CsvImporter.Analyze("a,b\n", NoExisting);
        Assert.Equal(1, result.Summary.Added);
    }

    [Theory]
    [InlineData("正面,背面,標籤")]
    [InlineData("front,back,tags")]
    [InlineData("Front,Back")]
    public void Skips_header_row_without_counting_it(string header)
    {
        var result = CsvImporter.Analyze(header + "\na,b\n", NoExisting);
        Assert.Equal(1, result.Summary.Added);
        Assert.Equal(0, result.Summary.Skipped);
        Assert.Empty(result.Summary.Errors);
    }

    [Fact]
    public void Header_only_detected_on_first_row()
    {
        var result = CsvImporter.Analyze("a,b\nfront,back\n", NoExisting);
        Assert.Equal(2, result.Summary.Added);
    }

    [Fact]
    public void Skips_duplicate_of_existing_card_ignoring_case_and_whitespace()
    {
        var existing = new HashSet<string> { CardText.FrontKey("Apple") };
        var result = CsvImporter.Analyze("  apple ,蘋果\nbanana,香蕉\n", existing);

        Assert.Equal(1, result.Summary.Added);
        Assert.Equal(1, result.Summary.Skipped);
        Assert.Equal(1, result.Summary.SkippedRows[0].LineNumber);
    }

    [Fact]
    public void Skips_duplicate_within_same_file()
    {
        var result = CsvImporter.Analyze("Cat,貓\ncat ,貓2\n", NoExisting);
        Assert.Equal(1, result.Summary.Added);
        Assert.Equal(1, result.Summary.Skipped);
        Assert.Equal("貓", result.NewCards[0].Back);
        Assert.Equal(2, result.Summary.SkippedRows[0].LineNumber);
        Assert.Contains("第 1 行", result.Summary.SkippedRows[0].Reason);
    }

    [Fact]
    public void Skip_reason_for_existing_card_mentions_deck()
    {
        var existing = new HashSet<string> { "cat" };
        var result = CsvImporter.Analyze("Cat,貓\n", existing);
        Assert.Contains("牌組", result.Summary.SkippedRows[0].Reason);
    }

    [Fact]
    public void Reports_errors_with_line_number_and_reason()
    {
        const string csv = "ok,fine\n" +      // 1
                           "onlyfront\n" +    // 2: too few
                           "a,b,c,d\n" +      // 3: too many
                           " ,back\n" +       // 4: empty front
                           "front, \n" +      // 5: empty back
                           "\"bad\"x,b\n";    // 6: quote error
        var result = CsvImporter.Analyze(csv, NoExisting);

        Assert.Equal(1, result.Summary.Added);
        var errors = result.Summary.Errors;
        Assert.Equal([2, 3, 4, 5, 6], errors.Select(e => e.LineNumber));
        Assert.Contains("欄位數", errors[0].Reason);
        Assert.Contains("欄位數", errors[1].Reason);
        Assert.Contains("正面", errors[2].Reason);
        Assert.Contains("背面", errors[3].Reason);
        Assert.Contains("引號", errors[4].Reason);
    }

    [Fact]
    public void Error_line_numbers_account_for_multiline_fields()
    {
        const string csv = "\"多行\n內容\",back\n" + // lines 1-2
                           "broken\n";              // line 3
        var result = CsvImporter.Analyze(csv, NoExisting);
        Assert.Equal(1, result.Summary.Added);
        Assert.Equal(3, result.Summary.Errors.Single().LineNumber);
        Assert.Equal("多行\n內容", result.NewCards[0].Front);
    }

    [Fact]
    public void Blank_lines_are_ignored_entirely()
    {
        var result = CsvImporter.Analyze("\na,b\n\n\nc,d\n\n", NoExisting);
        Assert.Equal(2, result.Summary.Added);
        Assert.Equal(0, result.Summary.Skipped);
        Assert.Empty(result.Summary.Errors);
    }

    [Fact]
    public void Trims_front_and_back_and_preserves_line_number_on_new_cards()
    {
        var result = CsvImporter.Analyze("x,y\n  front  ,  back  \n", NoExisting);
        Assert.Equal("front", result.NewCards[1].Front);
        Assert.Equal("back", result.NewCards[1].Back);
        Assert.Equal(2, result.NewCards[1].LineNumber);
    }

    [Fact]
    public void Tags_are_trimmed_deduplicated_and_empty_entries_dropped()
    {
        Assert.Equal(["N2", "名詞"], CardText.ParseTags(" N2 ; ;名詞;N2;"));
        Assert.Empty(CardText.ParseTags(""));
    }
}
