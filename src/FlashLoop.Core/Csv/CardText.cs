namespace FlashLoop.Core.Csv;

/// <summary>卡片文字的正規化規則。</summary>
public static class CardText
{
    public const char TagSeparator = ';';

    /// <summary>重複判斷用的 key：去除前後空白、英文字母不分大小寫。</summary>
    public static string FrontKey(string front) => front.Trim().ToLowerInvariant();

    public static IReadOnlyList<string> ParseTags(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(TagSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();

    public static string JoinTags(IEnumerable<string> tags) => string.Join(TagSeparator, tags);
}
