using FlashLoop.Core.Csv;

namespace FlashLoop.Core.Backup;

/// <summary>
/// 合併兩份資料（皆需已通過驗證）。規則：
/// 1. 牌組、卡片以 guid 對應；兩邊都有時取 UpdatedAt 較晚者（相同時保留本機）。
/// 2. 不同 guid 但同名的牌組都保留，後加入者名稱加上「 (2)」等後綴。
/// 3. 同一牌組內不同 guid 但正面相同的卡片，保留 UpdatedAt 較晚者，另一張連同其評分紀錄捨棄。
/// 4. 評分紀錄以 guid 聯集。
/// </summary>
public static class BackupMerger
{
    public static BackupDocument Merge(BackupDocument current, BackupDocument incoming)
    {
        var decks = UnionLatest(current.Decks, incoming.Decks, d => d.Guid, d => d.UpdatedAt);
        decks = MakeDeckNamesUnique(decks);

        var cards = UnionLatest(current.Cards, incoming.Cards, c => c.Guid, c => c.UpdatedAt);
        var frontWinners = cards
            .GroupBy(c => (c.DeckGuid, CardText.FrontKey(c.Front)))
            .Select(g => g.Aggregate((kept, next) => next.UpdatedAt > kept.UpdatedAt ? next : kept).Guid)
            .ToHashSet();
        // 以過濾方式保留 UnionLatest 的順序（新卡依此順序學習）
        cards = cards.Where(c => frontWinners.Contains(c.Guid)).ToList();

        var cardGuids = cards.Select(c => c.Guid).ToHashSet();
        var logs = current.ReviewLogs.Concat(incoming.ReviewLogs)
            .DistinctBy(l => l.Guid)
            .Where(l => cardGuids.Contains(l.CardGuid))
            .ToList();

        return current with { Decks = decks, Cards = cards, ReviewLogs = logs };
    }

    private static List<T> UnionLatest<T>(
        IEnumerable<T> current, IEnumerable<T> incoming, Func<T, Guid> key, Func<T, DateTimeOffset> updatedAt)
    {
        var order = new List<Guid>();
        var map = new Dictionary<Guid, T>();
        foreach (var item in current.Concat(incoming))
        {
            var k = key(item);
            if (!map.TryGetValue(k, out var existing))
            {
                order.Add(k);
                map[k] = item;
            }
            else if (updatedAt(item) > updatedAt(existing))
            {
                map[k] = item;
            }
        }
        return order.Select(k => map[k]).ToList();
    }

    private static List<BackupDeck> MakeDeckNamesUnique(List<BackupDeck> decks)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<BackupDeck>(decks.Count);
        foreach (var deck in decks)
        {
            var name = deck.Name.Trim();
            for (var n = 2; !used.Add(name); n++)
            {
                name = $"{deck.Name.Trim()} ({n})";
            }
            result.Add(deck with { Name = name });
        }
        return result;
    }
}
