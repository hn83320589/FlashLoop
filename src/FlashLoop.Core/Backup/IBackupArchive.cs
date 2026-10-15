namespace FlashLoop.Core.Backup;

/// <summary>覆蓋還原前保存目前資料的地方，讓誤按「覆蓋」仍能救回。</summary>
public interface IBackupArchive
{
    /// <summary>保存備份 JSON，回傳保存位置（顯示給使用者）。失敗時丟出例外，呼叫端不得繼續還原。</summary>
    string Save(string json, DateTimeOffset at);
}
