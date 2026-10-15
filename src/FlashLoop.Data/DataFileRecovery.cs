namespace FlashLoop.Data;

/// <summary>
/// 啟動時遇到損毀的設定檔或資料庫，先改名保留（不刪除），讓使用者能重新開始，事後仍可救回或回報。
/// </summary>
public static class DataFileRecovery
{
    // SQLite 的 rollback journal 必須跟著主檔移走，否則新資料庫開啟時會套用舊的 journal
    private static readonly string[] Sidecars = ["-journal", "-wal", "-shm"];

    /// <summary>把 path 改名為 path.yyyyMMdd-HHmmss.bak（已存在則加上 -2、-3…），回傳新路徑。</summary>
    public static string MoveAside(string path, DateTimeOffset at)
    {
        var baseName = $"{path}.{at:yyyyMMdd-HHmmss}";
        var target = $"{baseName}.bak";
        for (var n = 2; File.Exists(target); n++)
        {
            target = $"{baseName}-{n}.bak";
        }

        File.Move(path, target);
        foreach (var suffix in Sidecars)
        {
            if (File.Exists(path + suffix))
            {
                File.Move(path + suffix, target + suffix);
            }
        }
        return target;
    }
}
