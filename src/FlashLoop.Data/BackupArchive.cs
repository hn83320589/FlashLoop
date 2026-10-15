using System.Text;
using FlashLoop.Core.Backup;

namespace FlashLoop.Data;

/// <summary>將自動備份寫成 before-restore-yyyyMMdd-HHmmss.json；全部保留，不覆寫既有檔案。</summary>
public sealed class BackupArchive(string directory) : IBackupArchive
{
    public string Save(string json, DateTimeOffset at)
    {
        Directory.CreateDirectory(directory);
        var baseName = $"before-restore-{at:yyyyMMdd-HHmmss}";
        for (var n = 1; ; n++)
        {
            var path = Path.Combine(directory, n == 1 ? $"{baseName}.json" : $"{baseName}-{n}.json");
            try
            {
                // CreateNew：同一秒內再次還原時不會蓋掉前一份
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(json);
                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
    }
}
