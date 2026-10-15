namespace FlashLoop.Core.Time;

public static class TimeProviderExtensions
{
    /// <summary>依 TimeProvider 的本地時區取得「今天」。換日時間為本地午夜。</summary>
    public static DateOnly Today(this TimeProvider time) =>
        DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}
