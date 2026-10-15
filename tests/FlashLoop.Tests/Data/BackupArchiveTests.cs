using FlashLoop.Data;

namespace FlashLoop.Tests.Data;

public sealed class BackupArchiveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlashLoopTests", Guid.NewGuid().ToString("N"), "backups");
    private static readonly DateTimeOffset At = new(2026, 10, 15, 9, 30, 5, TimeSpan.FromHours(8));

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_directory)!;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Creates_directory_and_writes_timestamped_file()
    {
        var path = new BackupArchive(_directory).Save("{\"a\":1}", At);

        Assert.Equal(Path.Combine(_directory, "before-restore-20261015-093005.json"), path);
        Assert.Equal("{\"a\":1}", File.ReadAllText(path));
    }

    [Fact]
    public void Never_overwrites_an_existing_archive()
    {
        var archive = new BackupArchive(_directory);
        var first = archive.Save("first", At);
        var second = archive.Save("second", At);

        Assert.NotEqual(first, second);
        Assert.Equal("first", File.ReadAllText(first));
        Assert.Equal("second", File.ReadAllText(second));
    }
}
