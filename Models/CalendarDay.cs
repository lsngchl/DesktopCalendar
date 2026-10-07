using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DesktopCalendar.Models;

public sealed class CalendarDay : INotifyPropertyChanged
{
    private string _text = "";
    private bool _isEditing;

    public DateTime Date { get; init; }
    public bool IsPreviousMonth { get; init; }
    public bool IsNextMonth { get; init; }
    public bool IsToday { get; init; }
    public bool IsSunday { get; init; }
    public bool IsSaturday { get; init; }
    public bool IsHoliday { get; init; }

    public string DayText { get; init; } = "";
    public string HolidayName { get; init; } = "";
    public string Key => Date.ToString("yyyy-MM-dd");

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value)
            {
                return;
            }

            _isEditing = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
