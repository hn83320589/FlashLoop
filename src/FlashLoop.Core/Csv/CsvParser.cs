using System.Text;

namespace FlashLoop.Core.Csv;

/// <summary>一筆 CSV record。LineNumber 為 record 起始的實體行號（從 1 開始）；Error 不為 null 時表示此 record 格式錯誤。</summary>
public sealed record CsvRow(int LineNumber, IReadOnlyList<string> Fields, string? Error)
{
    public bool IsBlank => Error is null && Fields.Count == 1 && string.IsNullOrWhiteSpace(Fields[0]);
}

/// <summary>
/// RFC 4180 風格的 CSV 解析器：支援以雙引號包住含逗號、雙引號（以 "" 跳脫）與換行的欄位，
/// 接受 CRLF / LF / CR 換行。欄位內的換行一律正規化為 LF。
/// </summary>
public static class CsvParser
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>以 UTF-8 解碼（含或不含 BOM）。非有效 UTF-8 時丟出 InvalidDataException。</summary>
    public static string Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var offset = bytes.AsSpan().StartsWith(Utf8Bom) ? Utf8Bom.Length : 0;
        try
        {
            return StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException($"檔案不是有效的 UTF-8 編碼（位元組位置約 {ex.Index + offset}），請以 UTF-8 另存後再匯入。", ex);
        }
    }

    public static IEnumerable<CsvRow> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var reader = new Reader(text.StartsWith('﻿') ? text[1..] : text);
        while (reader.ReadRow() is { } row)
        {
            yield return row;
        }
    }

    private sealed class Reader(string text)
    {
        private int _pos;
        private int _line = 1;

        private bool AtEnd => _pos >= text.Length;

        public CsvRow? ReadRow()
        {
            if (AtEnd)
            {
                return null;
            }

            var startLine = _line;
            var fields = new List<string>();
            var field = new StringBuilder();

            while (true)
            {
                if (!AtEnd && text[_pos] == '"')
                {
                    _pos++;
                    if (!ReadQuotedField(field))
                    {
                        _pos = text.Length;
                        return new CsvRow(startLine, fields, "引號未關閉：此行開始的欄位直到檔案結尾都沒有結尾雙引號");
                    }

                    if (!AtEnd && text[_pos] is not (',' or '\r' or '\n'))
                    {
                        var unexpected = text[_pos];
                        SkipRestOfLine();
                        return new CsvRow(startLine, fields, $"結尾雙引號後出現多餘字元「{unexpected}」（欄位內的雙引號需寫成 \"\"）");
                    }
                }
                else
                {
                    while (!AtEnd && text[_pos] is not (',' or '\r' or '\n'))
                    {
                        field.Append(text[_pos++]);
                    }
                }

                fields.Add(field.ToString());
                field.Clear();

                if (AtEnd)
                {
                    return new CsvRow(startLine, fields, null);
                }

                if (text[_pos] == ',')
                {
                    _pos++;
                    continue;
                }

                ConsumeNewline();
                return new CsvRow(startLine, fields, null);
            }
        }

        /// <summary>讀取引號欄位內容（開頭引號已消耗）。回傳 false 表示檔案結束仍未關閉。</summary>
        private bool ReadQuotedField(StringBuilder field)
        {
            while (!AtEnd)
            {
                var c = text[_pos];
                if (c == '"')
                {
                    if (_pos + 1 < text.Length && text[_pos + 1] == '"')
                    {
                        field.Append('"');
                        _pos += 2;
                        continue;
                    }
                    _pos++;
                    return true;
                }

                if (c is '\r' or '\n')
                {
                    ConsumeNewline();
                    field.Append('\n');
                    continue;
                }

                field.Append(c);
                _pos++;
            }
            return false;
        }

        private void SkipRestOfLine()
        {
            while (!AtEnd && text[_pos] is not ('\r' or '\n'))
            {
                _pos++;
            }
            if (!AtEnd)
            {
                ConsumeNewline();
            }
        }

        private void ConsumeNewline()
        {
            _pos += text[_pos] == '\r' && _pos + 1 < text.Length && text[_pos + 1] == '\n' ? 2 : 1;
            _line++;
        }
    }
}
