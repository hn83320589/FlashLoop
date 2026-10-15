namespace FlashLoop.Core.Backup;

public interface IBackupStore
{
    /// <summary>匯出全部牌組、卡片與評分紀錄（不含設定）。</summary>
    BackupDocument ExportAll();

    /// <summary>以單一 transaction 清空現有資料並寫入文件內容。文件需先通過驗證。</summary>
    void ReplaceAll(BackupDocument document);
}
