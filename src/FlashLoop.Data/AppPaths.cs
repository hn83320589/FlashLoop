namespace FlashLoop.Data;

/// <summary>資料庫與設定檔位置：%LocalAppData%\FlashLoop\。</summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashLoop");

    public static string DatabaseFile => Path.Combine(DataDirectory, "flashloop.db");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
