using System.Runtime.ExceptionServices;

namespace FlashLoop.App.Tests.Support;

/// <summary>WinForms 控制項需要在 STA 執行緒上建立與操作；xUnit 的工作執行緒是 MTA。</summary>
internal static class Sta
{
    public static void Run(Action<AppHarness> test) => Run(() =>
    {
        using var app = new AppHarness();
        test(app);
    });

    public static void Run(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
