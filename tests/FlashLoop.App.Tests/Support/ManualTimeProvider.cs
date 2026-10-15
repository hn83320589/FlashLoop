namespace FlashLoop.App.Tests.Support;

/// <summary>可手動推進的時間來源，固定使用 UTC+8，讓本機與 CI（UTC）算出相同的本地日期。</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private static readonly TimeZoneInfo Taipei =
        TimeZoneInfo.CreateCustomTimeZone("Test+8", TimeSpan.FromHours(8), "Test+8", "Test+8");

    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTime local) =>
        _utcNow = new DateTimeOffset(local, Taipei.BaseUtcOffset).ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => Taipei;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void AdvanceDays(int days) => _utcNow = _utcNow.AddDays(days);
}
