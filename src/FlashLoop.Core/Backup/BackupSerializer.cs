using System.Text.Encodings.Web;
using System.Text.Json;

namespace FlashLoop.Core.Backup;

public static class BackupSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // 讓日文、中文以原字輸出，備份檔可直接閱讀
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(BackupDocument document) => JsonSerializer.Serialize(document, Options);

    /// <summary>解析 JSON；格式錯誤時丟出 InvalidDataException。不做內容驗證（見 BackupValidator）。</summary>
    public static BackupDocument Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BackupDocument>(json, Options)
                ?? throw new InvalidDataException("備份檔內容為空");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"備份檔不是有效的 JSON：{ex.Message}", ex);
        }
    }
}
