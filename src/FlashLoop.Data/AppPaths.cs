namespace FlashLoop.Data;

/// <summary>
/// 資料庫與設定檔位置：預設為 %LocalAppData%\FlashLoop\。
/// 可用環境變數 FLASHLOOP_DATA_DIR 覆寫（供展示、截圖或測試使用，不影響正式資料）。
/// </summary>
public static class AppPaths
{
    public const string DataDirOverrideVariable = "FLASHLOOP_DATA_DIR";

    public static string DataDirectory { get; } =
        Environment.GetEnvironmentVariable(DataDirOverrideVariable) is { Length: > 0 } overrideDir
            ? Path.GetFullPath(overrideDir)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashLoop");

    public static string DatabaseFile => Path.Combine(DataDirectory, "flashloop.db");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
