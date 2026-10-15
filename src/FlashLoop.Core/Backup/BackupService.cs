using FlashLoop.Core.Model;
using FlashLoop.Core.Persistence;

namespace FlashLoop.Core.Backup;

public enum RestoreMode
{
    /// <summary>清空本機資料後還原備份內容，並套用備份中的設定。</summary>
    Overwrite,

    /// <summary>與本機資料合併，以較晚更新者為準；保留本機設定。</summary>
    Merge,
}

public sealed class BackupService(IBackupStore store, ISettingsStore settings, TimeProvider time)
{
    public string Export()
    {
        var limits = settings.LoadLimits();
        var doc = store.ExportAll() with
        {
            ExportedAt = time.GetUtcNow(),
            Settings = new BackupSettings(limits.NewCardsPerDay, limits.ReviewsPerDay),
        };
        return BackupSerializer.Serialize(doc);
    }

    /// <summary>還原備份。格式或內容有誤時丟出 InvalidDataException，且不會變更任何資料。</summary>
    public void Restore(string json, RestoreMode mode)
    {
        var incoming = BackupSerializer.Deserialize(json);
        BackupValidator.Validate(incoming);

        switch (mode)
        {
            case RestoreMode.Overwrite:
                store.ReplaceAll(incoming);
                if (incoming.Settings is { } s)
                {
                    settings.SaveLimits(new DailyLimits(s.NewCardsPerDay, s.ReviewsPerDay));
                }
                break;

            case RestoreMode.Merge:
                var merged = BackupMerger.Merge(store.ExportAll(), incoming);
                BackupValidator.Validate(merged);
                store.ReplaceAll(merged);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }
}
