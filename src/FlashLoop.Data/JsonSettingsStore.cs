using System.Text.Json;
using FlashLoop.Core.Model;
using FlashLoop.Core.Persistence;

namespace FlashLoop.Data;

/// <summary>將全域設定存成 settings.json。檔案不存在時回傳預設值。</summary>
public sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public DailyLimits LoadLimits()
    {
        if (!File.Exists(filePath))
        {
            return DailyLimits.Default;
        }

        SettingsFile? file;
        try
        {
            file = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(filePath), Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"設定檔格式錯誤：{filePath}（{ex.Message}）", ex);
        }

        if (file is null || file.NewCardsPerDay < 0 || file.ReviewsPerDay < 0)
        {
            throw new InvalidDataException($"設定檔內容無效：{filePath}");
        }
        return new DailyLimits(file.NewCardsPerDay, file.ReviewsPerDay);
    }

    public void SaveLimits(DailyLimits limits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limits.NewCardsPerDay);
        ArgumentOutOfRangeException.ThrowIfNegative(limits.ReviewsPerDay);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        File.WriteAllText(filePath, JsonSerializer.Serialize(new SettingsFile(limits.NewCardsPerDay, limits.ReviewsPerDay), Options));
    }

    private sealed record SettingsFile(int NewCardsPerDay, int ReviewsPerDay);
}
