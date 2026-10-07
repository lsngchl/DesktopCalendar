using System.Windows.Media;

namespace DesktopCalendar.Models;

public sealed class DayEvent
{
    public string Text { get; init; } = "";
    public bool IsAllDay { get; init; }
    public DateTime Start { get; init; }
    public Brush Color { get; init; } = Brushes.Transparent;
    public Brush Background { get; init; } = Brushes.Transparent;
}
