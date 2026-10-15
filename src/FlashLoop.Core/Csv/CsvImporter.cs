namespace FlashLoop.Core.Csv;

public sealed record ImportedCard(int LineNumber, string Front, string Back, IReadOnlyList<string> Tags);

public sealed record ImportIssue(int LineNumber, string Reason);

public sealed record ImportSummary(int Added, IReadOnlyList<ImportIssue> SkippedRows, IReadOnlyList<ImportIssue> Errors)
{
    public int Skipped => SkippedRows.Count;
}

public sealed record ImportAnalysis(IReadOnlyList<ImportedCard> NewCards, ImportSummary Summary);

/// <summary>
/// 將 CSV（正面,背面,標籤）轉成待新增卡片與匯入摘要。純邏輯，不碰資料庫：
/// 呼叫端提供該牌組既有的 front key 集合。
/// </summary>
public static class CsvImporter
{
    private static readonly string[][] HeaderPatterns =
    [
        ["正面", "背面", "標籤"],
        ["front", "back", "tags"],
    ];

    public static ImportAnalysis Analyze(byte[] bytes, IReadOnlySet<string> existingFrontKeys) =>
        Analyze(CsvParser.Decode(bytes), existingFrontKeys);

    public static ImportAnalysis Analyze(string text, IReadOnlySet<string> existingFrontKeys)
    {
        ArgumentNullException.ThrowIfNull(existingFrontKeys);

        var cards = new List<ImportedCard>();
        var skipped = new List<ImportIssue>();
        var errors = new List<ImportIssue>();
        var linesByKey = new Dictionary<string, int>(StringComparer.Ordinal); // 檔案內已出現的 key → 行號
        var isFirstContentRow = true;

        foreach (var row in CsvParser.Parse(text))
        {
            if (row.IsBlank)
            {
                continue;
            }

            var wasFirst = isFirstContentRow;
            isFirstContentRow = false;

            if (row.Error is not null)
            {
                errors.Add(new ImportIssue(row.LineNumber, row.Error));
                continue;
            }

            if (wasFirst && IsHeader(row.Fields))
            {
                continue;
            }

            if (Validate(row.Fields) is { } reason)
            {
                errors.Add(new ImportIssue(row.LineNumber, reason));
                continue;
            }

            var front = row.Fields[0].Trim();
            var key = CardText.FrontKey(front);
            if (existingFrontKeys.Contains(key))
            {
                skipped.Add(new ImportIssue(row.LineNumber, $"正面「{front}」已存在於此牌組"));
                continue;
            }
            if (linesByKey.TryGetValue(key, out var firstLine))
            {
                skipped.Add(new ImportIssue(row.LineNumber, $"正面「{front}」與檔案第 {firstLine} 行重複"));
                continue;
            }
            linesByKey[key] = row.LineNumber;

            var tags = row.Fields.Count > 2 ? CardText.ParseTags(row.Fields[2]) : [];
            cards.Add(new ImportedCard(row.LineNumber, front, row.Fields[1].Trim(), tags));
        }

        return new ImportAnalysis(cards, new ImportSummary(cards.Count, skipped, errors));
    }

    private static string? Validate(IReadOnlyList<string> fields)
    {
        if (fields.Count is < 2 or > 3)
        {
            return $"欄位數為 {fields.Count}，應為 2 或 3（正面,背面,標籤）";
        }
        if (string.IsNullOrWhiteSpace(fields[0]))
        {
            return "正面為空白";
        }
        if (string.IsNullOrWhiteSpace(fields[1]))
        {
            return "背面為空白";
        }
        return null;
    }

    private static bool IsHeader(IReadOnlyList<string> fields)
    {
        if (fields.Count is < 2 or > 3)
        {
            return false;
        }
        return HeaderPatterns.Any(pattern =>
            fields.Select((f, i) => string.Equals(f.Trim(), pattern[i], StringComparison.OrdinalIgnoreCase)).All(x => x));
    }
}
