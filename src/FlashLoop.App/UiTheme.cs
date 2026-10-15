namespace FlashLoop.App;

internal static class UiTheme
{
    public static readonly Color Background = Color.FromArgb(247, 247, 245);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(222, 222, 218);
    public static readonly Color Text = Color.FromArgb(33, 37, 41);
    public static readonly Color MutedText = Color.FromArgb(108, 117, 125);
    public static readonly Color Accent = Color.FromArgb(59, 110, 168);

    public static readonly Color Again = Color.FromArgb(192, 57, 43);
    public static readonly Color Hard = Color.FromArgb(211, 132, 32);
    public static readonly Color Good = Color.FromArgb(39, 133, 86);
    public static readonly Color Easy = Color.FromArgb(41, 112, 179);

    public static readonly Font Base = new("Microsoft JhengHei UI", 10f);
    public static readonly Font Heading = new("Microsoft JhengHei UI", 14f, FontStyle.Bold);
    public static readonly Font Big = new("Microsoft JhengHei UI", 22f, FontStyle.Bold);

    // Yu Gothic UI 對日文字形較佳，中文 fallback 由系統處理
    public static readonly Font CardFront = new("Yu Gothic UI", 32f, FontStyle.Bold);
    public static readonly Font CardBack = new("Yu Gothic UI", 20f);
}
