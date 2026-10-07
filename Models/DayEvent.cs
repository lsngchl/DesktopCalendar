using System.Windows.Media;

namespace DesktopCalendar.Models;

public sealed class DayEvent
{
    public string Time { get; init; } = "";
    public string Title { get; init; } = "";
    public string Text => Time + Title;
    public bool IsAllDay { get; init; }
    public DateTime Start { get; init; }
    public Brush Color { get; init; } = Brushes.Transparent;
    public Brush Background { get; init; } = Brushes.Transparent;
}
