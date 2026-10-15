using System.Diagnostics.CodeAnalysis;
using FlashLoop.Core.Csv;
using FlashLoop.Core.Scheduling;

namespace FlashLoop.Core.Backup;

/// <summary>寫入資料庫前驗證備份內容，任何問題都以 InvalidDataException 回報，避免還原到一半失敗。</summary>
public static class BackupValidator
{
    public static void Validate(BackupDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);
        if (doc.Version != BackupDocument.CurrentVersion)
        {
            Fail($"不支援的備份版本 {doc.Version}（目前支援 {BackupDocument.CurrentVersion}）");
        }
        if (doc.Decks is null || doc.Cards is null || doc.ReviewLogs is null)
        {
            Fail("缺少 decks、cards 或 reviewLogs 欄位");
        }
        if (doc.Settings is { } s && (s.NewCardsPerDay < 0 || s.ReviewsPerDay < 0))
        {
            Fail("設定中的每日上限不可為負數");
        }

        var deckGuids = new HashSet<Guid>();
        var deckNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in doc.Decks)
        {
            if (string.IsNullOrWhiteSpace(d.Name))
            {
                Fail($"牌組 {d.Guid} 名稱為空白");
            }
            if (!deckGuids.Add(d.Guid))
            {
                Fail($"牌組 guid 重複：{d.Guid}");
            }
            if (!deckNames.Add(d.Name.Trim()))
            {
                Fail($"牌組名稱重複：{d.Name}");
            }
        }

        var cardGuids = new HashSet<Guid>();
        var frontKeys = new HashSet<(Guid, string)>();
        foreach (var c in doc.Cards)
        {
            if (!cardGuids.Add(c.Guid))
            {
                Fail($"卡片 guid 重複：{c.Guid}");
            }
            if (!deckGuids.Contains(c.DeckGuid))
            {
                Fail($"卡片 {c.Guid} 所屬的牌組 {c.DeckGuid} 不存在");
            }
            if (string.IsNullOrWhiteSpace(c.Front) || string.IsNullOrWhiteSpace(c.Back))
            {
                Fail($"卡片 {c.Guid} 的正面或背面為空白");
            }
            if (!frontKeys.Add((c.DeckGuid, CardText.FrontKey(c.Front))))
            {
                Fail($"同一牌組內有重複的正面：「{c.Front.Trim()}」");
            }
            if (c.Ease < CardSchedule.MinEase || c.Repetitions < 0 || c.Interval < 0)
            {
                Fail($"卡片 {c.Guid} 的排程數值無效（ease ≥ {CardSchedule.MinEase}、repetitions 與 interval ≥ 0）");
            }
        }

        var logGuids = new HashSet<Guid>();
        foreach (var l in doc.ReviewLogs)
        {
            if (!logGuids.Add(l.Guid))
            {
                Fail($"評分紀錄 guid 重複：{l.Guid}");
            }
            if (!cardGuids.Contains(l.CardGuid))
            {
                Fail($"評分紀錄 {l.Guid} 對應的卡片 {l.CardGuid} 不存在");
            }
            if (!Enum.IsDefined((Rating)l.Rating))
            {
                Fail($"評分紀錄 {l.Guid} 的評分 {l.Rating} 無效（應為 1 到 4）");
            }
        }
    }

    [DoesNotReturn]
    private static void Fail(string message) => throw new InvalidDataException($"備份檔驗證失敗：{message}");
}
