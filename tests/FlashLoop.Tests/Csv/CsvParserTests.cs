using System.Text;
using FlashLoop.Core.Csv;

namespace FlashLoop.Tests.Csv;

public class CsvParserTests
{
    private static List<CsvRow> Parse(string text) => CsvParser.Parse(text).ToList();

    [Fact]
    public void Parses_simple_rows_with_line_numbers()
    {
        var rows = Parse("a,b,c\nd,e,f\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b", "c"], rows[0].Fields);
        Assert.Equal(1, rows[0].LineNumber);
        Assert.Equal(["d", "e", "f"], rows[1].Fields);
        Assert.Equal(2, rows[1].LineNumber);
    }

    [Fact]
    public void Handles_comma_inside_quoted_field()
    {
        var rows = Parse("\"a,b\",c\n");
        Assert.Equal(["a,b", "c"], rows[0].Fields);
    }

    [Fact]
    public void Handles_escaped_double_quotes()
    {
        var rows = Parse("\"say \"\"hi\"\"\",x\n");
        Assert.Equal(["say \"hi\"", "x"], rows[0].Fields);
    }

    [Fact]
    public void Handles_newline_inside_quoted_field_and_keeps_line_numbers()
    {
        var rows = Parse("\"line1\nline2\",b\r\nnext,c\n");
        Assert.Equal(2, rows.Count);
        Assert.Equal("line1\nline2", rows[0].Fields[0]);
        Assert.Equal(1, rows[0].LineNumber);
        Assert.Equal(3, rows[1].LineNumber);
    }

    [Fact]
    public void Normalizes_crlf_inside_quoted_field_to_lf()
    {
        var rows = Parse("\"x\r\ny\",b");
        Assert.Equal("x\ny", rows[0].Fields[0]);
    }

    [Fact]
    public void Supports_crlf_and_cr_line_endings_and_missing_final_newline()
    {
        var rows = Parse("a,b\r\nc,d\re,f");
        Assert.Equal(3, rows.Count);
        Assert.Equal(["e", "f"], rows[2].Fields);
        Assert.Equal(3, rows[2].LineNumber);
    }

    [Fact]
    public void Keeps_empty_trailing_field()
    {
        var rows = Parse("a,b,\n");
        Assert.Equal(["a", "b", ""], rows[0].Fields);
    }

    [Fact]
    public void Marks_blank_lines()
    {
        var rows = Parse("a,b\n\n   \nc,d\n");
        Assert.Equal(4, rows.Count);
        Assert.True(rows[1].IsBlank);
        Assert.True(rows[2].IsBlank);
        Assert.Equal(4, rows[3].LineNumber);
    }

    [Fact]
    public void Reports_text_after_closing_quote_and_continues_with_next_line()
    {
        var rows = Parse("\"a\"x,b\nc,d\n");
        Assert.NotNull(rows[0].Error);
        Assert.Equal(1, rows[0].LineNumber);
        Assert.Null(rows[1].Error);
        Assert.Equal(["c", "d"], rows[1].Fields);
    }

    [Fact]
    public void Reports_unclosed_quote_at_its_starting_line()
    {
        var rows = Parse("a,b\n\"never closed,c\nmore\n");
        Assert.Equal(2, rows.Count);
        Assert.NotNull(rows[1].Error);
        Assert.Equal(2, rows[1].LineNumber);
        Assert.Contains("引號", rows[1].Error);
    }

    [Fact]
    public void Decodes_utf8_with_bom()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("勉強,べんきょう\n")).ToArray();
        var text = CsvParser.Decode(bytes);
        Assert.Equal("勉強,べんきょう\n", text);
        Assert.Equal("勉強", CsvParser.Parse(text).Single().Fields[0]);
    }

    [Fact]
    public void Decodes_utf8_without_bom()
    {
        var bytes = Encoding.UTF8.GetBytes("勉強,べんきょう\n");
        Assert.Equal("勉強,べんきょう\n", CsvParser.Decode(bytes));
    }

    [Fact]
    public void Rejects_invalid_utf8()
    {
        byte[] bytes = [0x61, 0x2C, 0xFF, 0xFE, 0x0A];
        var ex = Assert.Throws<InvalidDataException>(() => CsvParser.Decode(bytes));
        Assert.Contains("UTF-8", ex.Message);
    }
}
