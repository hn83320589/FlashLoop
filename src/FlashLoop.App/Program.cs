using FlashLoop.Core.Backup;
using FlashLoop.Core.Model;
using FlashLoop.Core.Services;
using FlashLoop.Data;

namespace FlashLoop.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ShowFatal(e.Exception);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Directory.CreateDirectory(AppPaths.DataDirectory);
        var settings = new JsonSettingsStore(AppPaths.SettingsFile);
        EnsureSettingsReadable(settings);

        var store = new SqliteStore(AppPaths.DatabaseFile);
        var time = TimeProvider.System;
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

    private static void EnsureSettingsReadable(JsonSettingsStore settings)
    {
        try
        {
            settings.LoadLimits();
        }
        catch (InvalidDataException ex)
        {
            MessageBox.Show(
                $"{ex.Message}\n\n將重設為預設值（每日新卡 {DailyLimits.Default.NewCardsPerDay}、複習 {DailyLimits.Default.ReviewsPerDay}）。",
                "FlashLoop", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            settings.SaveLimits(DailyLimits.Default);
        }
    }

    private static void ShowFatal(Exception ex) =>
        MessageBox.Show($"發生未預期的錯誤：\n{ex.Message}\n\n{ex.GetType().Name}", "FlashLoop",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
}
