namespace FlashLoop.Tests.Support;

/// <summary>可控制的時間來源，固定使用 UTC+8 時區以驗證本地日期邏輯。</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private static readonly TimeZoneInfo Taipei =
        TimeZoneInfo.CreateCustomTimeZone("Test+8", TimeSpan.FromHours(8), "Test+8", "Test+8");

    private DateTimeOffset _utcNow;

    /// <summary>以本地（UTC+8）時間建立。</summary>
    public FakeTimeProvider(int year, int month, int day, int hour = 9, int minute = 0)
    {
        SetLocal(new DateTime(year, month, day, hour, minute, 0));
    }

    public override TimeZoneInfo LocalTimeZone => Taipei;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetLocal(DateTime local) =>
        _utcNow = new DateTimeOffset(local, Taipei.BaseUtcOffset).ToUniversalTime();

    public void Advance(TimeSpan by) => _utcNow += by;

    public void AdvanceDays(int days) => Advance(TimeSpan.FromDays(days));
}
