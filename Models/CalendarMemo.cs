namespace DesktopCalendar.Models;

public sealed class CalendarMemo
{
    public string Text { get; set; } = "";
    public bool Deleted { get; set; }
    public string UpdatedAtUtc { get; set; } = "";
}

public sealed class DriveMemoStore
{
    public Dictionary<string, CalendarMemo> Events { get; set; } = new(StringComparer.Ordinal);
}
