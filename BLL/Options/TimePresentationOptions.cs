namespace BLL.Options;

public sealed class TimePresentationOptions
{
    public const string SectionName = "TimePresentation";

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public int OffsetMinutes { get; set; } = 420;
    public bool UseVietnamOffset { get; set; } = true;
}
