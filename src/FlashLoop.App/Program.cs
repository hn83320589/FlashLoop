namespace FlashLoop.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Form { Text = "FlashLoop" });
    }
}
