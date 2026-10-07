namespace DesktopCalendar.Models;

public sealed class CalendarDay
{
    public DateTime Date { get; init; }
    public bool IsPreviousMonth { get; init; }
    public bool IsNextMonth { get; init; }
    public bool IsToday { get; init; }
    public bool IsSunday { get; init; }
    public bool IsSaturday { get; init; }
    public bool IsHoliday { get; init; }

    public string DayText { get; init; } = "";
    public string HolidayName { get; init; } = "";
    public IReadOnlyList<DayEvent> Events { get; init; } = [];
    public string MoreText { get; init; } = "";
}
