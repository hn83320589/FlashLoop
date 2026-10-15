using FlashLoop.Core.Backup;
using FlashLoop.Core.Model;
using FlashLoop.Core.Services;
using FlashLoop.Data;
using Microsoft.Data.Sqlite;

namespace FlashLoop.App;

static class Program
{
    // SQLITE_BUSY、SQLITE_LOCKED：檔案本身沒壞，只是被占用，不能當成損毀改名
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        var time = TimeProvider.System;
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowStartupError($"無法建立資料夾：\n{AppPaths.DataDirectory}\n\n{ex.Message}");
            return;
        }

        var settings = new JsonSettingsStore(AppPaths.SettingsFile);
        if (!EnsureSettingsReadable(settings, time))
        {
            return;
        }

        var store = OpenDatabase(time);
        if (store is null)
        {
            return;
        }

        var services = new AppServices(
            new DeckService(store, store, store, settings, time),
            new ImportService(store, time),
            new ReviewService(store, store, store, settings, time),
            new StatsService(store, store, time),
            new BackupService(store, settings, new BackupArchive(AppPaths.BackupsDirectory), time),
            settings,
            time);

        Application.Run(new MainForm(services));
    }

    /// <summary>設定檔損毀時讓使用者選擇重設或離開。回傳 false 表示應結束程式。</summary>
    private static bool EnsureSettingsReadable(JsonSettingsStore settings, TimeProvider time)
    {
        try
        {
            settings.LoadLimits();
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            var reset = AskResetOrExit(
                "無法讀取設定檔",
                ex.Message,
                $"重設：原設定檔改名保留，改用預設值（每日新卡 {DailyLimits.Default.NewCardsPerDay}、" +
                $"複習 {DailyLimits.Default.ReviewsPerDay}）。牌組與學習進度不受影響。");
            if (!reset)
            {
                return false;
            }

            try
            {
                var backup = DataFileRecovery.MoveAside(AppPaths.SettingsFile, time.GetLocalNow());
                settings.SaveLimits(DailyLimits.Default);
                ShowInfo($"設定已重設為預設值。\n\n原設定檔保留在：\n{backup}");
                return true;
            }
            catch (Exception resetError) when (resetError is IOException or UnauthorizedAccessException)
            {
                ShowStartupError($"無法重設設定檔：\n{resetError.Message}");
                return false;
            }
        }
    }

    /// <summary>資料庫無法開啟時讓使用者選擇重設或離開。回傳 null 表示應結束程式。</summary>
    private static SqliteStore? OpenDatabase(TimeProvider time)
    {
        try
        {
            return new SqliteStore(AppPaths.DatabaseFile);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is SqliteBusy or SqliteLocked)
        {
            ShowStartupError($"資料庫正被其他程式使用：\n{AppPaths.DatabaseFile}\n\n請關閉其他 FlashLoop 視窗後再試。");
            return null;
        }
        catch (SqliteException ex)
        {
            var reset = AskResetOrExit(
                "無法開啟資料庫",
                $"{AppPaths.DatabaseFile}\n\n{ex.Message}",
                "重設：原資料庫改名保留，建立新的空白資料庫，牌組與學習進度會從頭開始。\n" +
                "若曾匯出備份，之後可從「備份 → 還原備份」救回。");
            if (!reset)
            {
                return null;
            }

            try
            {
                SqliteConnection.ClearAllPools();
                var backup = DataFileRecovery.MoveAside(AppPaths.DatabaseFile, time.GetLocalNow());
                var store = new SqliteStore(AppPaths.DatabaseFile);
                ShowInfo($"已建立新的資料庫。\n\n原資料庫保留在：\n{backup}");
                return store;
            }
            catch (Exception resetError) when (resetError is SqliteException or IOException or UnauthorizedAccessException)
            {
                ShowStartupError($"無法重設資料庫：\n{resetError.Message}");
                return null;
            }
        }
    }

    private static bool AskResetOrExit(string heading, string detail, string resetDescription)
    {
        var reset = new TaskDialogButton("重設");
        var exit = new TaskDialogButton("離開");
        var page = new TaskDialogPage
        {
            Caption = "FlashLoop",
            Heading = heading,
            Text = $"{detail}\n\n{resetDescription}\n\n離開：不做任何變更，結束程式。",
            Icon = TaskDialogIcon.Warning,
            Buttons = { reset, exit },
            DefaultButton = exit,
        };
        return TaskDialog.ShowDialog(page) == reset;
    }

    private static void ShowInfo(string message) =>
        MessageBox.Show(message, "FlashLoop", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static void ShowStartupError(string message) =>
        MessageBox.Show(message, "FlashLoop 無法啟動", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static void ShowFatal(Exception ex) =>
        MessageBox.Show($"發生未預期的錯誤：\n{ex.Message}\n\n{ex.GetType().Name}", "FlashLoop",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
}
