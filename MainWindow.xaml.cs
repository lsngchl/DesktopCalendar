using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCalendar.Models;
using DesktopCalendar.Services;

namespace DesktopCalendar;

public partial class MainWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int ResizeBorderPixels = 12;
    private const int WmMouseActivate = 0x0021;
    private const int WmLeftButtonDoubleClick = 0x0203;
    private const int MaActivate = 1;
    private const int MaNoActivate = 3;
    // Layout sizes mirrored from MainWindow.xaml, used to decide how many events fit in a day cell.
    private const double CellVerticalChrome = 17;
    private const double CellHorizontalChrome = 30;
    private const double DateLineHeight = 24;
    private const double HolidayLineHeight = 20;
    private const double EventFontSize = 12;
    private const double EventLineHeight = 16;
    private const double EventChromeHeight = 3;
    private const double EventDotWidth = 11;
    private const double MoreLineHeight = 16;
    private static readonly TimeSpan FeedRefreshInterval = TimeSpan.FromMinutes(5);

    private readonly SettingsService _settingsService = new();
    private readonly KoreanHolidayService _holidayService = new();
    private readonly CalendarFeedService _feeds = new();
    private readonly DesktopHostService _desktopHost = new();
    private readonly DispatcherTimer _desktopRepairTimer = new();
    private readonly DispatcherTimer _feedRefreshTimer = new();

    private AppSettings _settings;
    private DateTime _today = DateTime.Today;
    private DateTime _currentMonth;
    private DateTime? _eventsRangeStart;
    private Dictionary<DateOnly, List<DayEvent>> _events = [];
    private double _cellWidth;
    private double _cellHeight;
    private bool _isRefreshing;
    private bool _suppressBoundsSave;
    private bool _isDraggingShell;
    private Point _dragStartScreen;
    private double _dragStartLeft;
    private double _dragStartTop;

    public MainWindow()
    {
        _settings = _settingsService.Load();
        _currentMonth = new DateTime(_today.Year, _today.Month, 1);
        _feeds.LoadCache();

        DataContext = this;
        InitializeComponent();

        ApplySavedBounds();
        BuildDays();
        ConfigureTimers();
    }

    public ObservableCollection<CalendarDay> Days { get; } = [];

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(AttachToDesktop, DispatcherPriority.ApplicationIdle);
        await RefreshFeedsAsync();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
        WindowBackdropService.EnableAcrylic(this);
        ShowInTaskbar = false;
        Topmost = false;
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => _desktopHost.Repair(this), DispatcherPriority.ContextIdle);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveCurrentBounds();
        _settingsService.Save(_settings);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SaveCurrentBounds();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        SaveCurrentBounds();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            WindowState = WindowState.Normal;
            AttachToDesktop();
        });
    }

    private void ConfigureTimers()
    {
        _desktopRepairTimer.Interval = TimeSpan.FromSeconds(3);
        _desktopRepairTimer.Tick += (_, _) => _desktopHost.Repair(this);
        _desktopRepairTimer.Start();

        _feedRefreshTimer.Interval = FeedRefreshInterval;
        _feedRefreshTimer.Tick += async (_, _) => await RefreshFeedsAsync();
        _feedRefreshTimer.Start();
    }

    private async Task RefreshFeedsAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            var succeeded = await _feeds.RefreshAsync();
            StatusText.Text = !succeeded ? $"{DateTime.Now:HH:mm} 갱신 실패"
                : !_feeds.HasSources ? "calendars.json에 캘린더 주소를 넣으세요"
                : "";
            FollowToday();
            _eventsRangeStart = null;
            BuildDays();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void FollowToday()
    {
        var today = DateTime.Today;
        if (today == _today)
        {
            return;
        }

        if (_currentMonth == new DateTime(_today.Year, _today.Month, 1))
        {
            _currentMonth = new DateTime(today.Year, today.Month, 1);
        }

        _today = today;
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        _currentMonth = _currentMonth.AddMonths(-1);
        BuildDays();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _currentMonth = new DateTime(_today.Year, _today.Month, 1);
        BuildDays();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _currentMonth = _currentMonth.AddMonths(1);
        BuildDays();
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            _isDraggingShell = true;
            _dragStartScreen = PointToScreen(e.GetPosition(this));
            _dragStartLeft = _settings.WindowLeft;
            _dragStartTop = _settings.WindowTop;
            Mouse.Capture(this);
            e.Handled = true;
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingShell || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        _settings.WindowLeft = _dragStartLeft + (current.X - _dragStartScreen.X) / Math.Max(1, dpi.DpiScaleX);
        _settings.WindowTop = _dragStartTop + (current.Y - _dragStartScreen.Y) / Math.Max(1, dpi.DpiScaleY);
        ClampSettingsToVirtualScreen();

        _suppressBoundsSave = true;
        ApplySavedBounds();
        _desktopHost.Repair(this);
        _suppressBoundsSave = false;
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingShell)
        {
            return;
        }

        _isDraggingShell = false;
        Mouse.Capture(null);
        SaveCurrentBounds();
        _settingsService.Save(_settings);
        e.Handled = true;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshFeedsAsync();
    }

    private void DayCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: CalendarDay day })
        {
            return;
        }

        var date = day.Date;
        Process.Start(new ProcessStartInfo($"https://calendar.google.com/calendar/r/day/{date.Year}/{date.Month}/{date.Day}")
        {
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private void DaysGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var cellWidth = Math.Floor(e.NewSize.Width / 7);
        var cellHeight = Math.Floor(e.NewSize.Height / 6);
        if (cellWidth == _cellWidth && cellHeight == _cellHeight)
        {
            return;
        }

        _cellWidth = cellWidth;
        _cellHeight = cellHeight;
        BuildDays();
    }

    private void BuildDays()
    {
        var first = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
        var start = first.AddDays(-(int)first.DayOfWeek);
        var last = start.AddDays(41);
        var holidays = _holidayService.GetHolidays(start, last, _feeds.Holidays);
        MonthRun.Text = $"{first.Month}월";
        YearRun.Text = $" {first.Year}";
        if (_eventsRangeStart != start)
        {
            _events = _feeds.GetEvents(DateOnly.FromDateTime(start), DateOnly.FromDateTime(last));
            _eventsRangeStart = start;
        }

        Days.Clear();
        for (var offset = 0; offset < 42; offset++)
        {
            var date = start.AddDays(offset);
            var monthStart = new DateTime(date.Year, date.Month, 1);
            holidays.TryGetValue(date.Date, out var holidayName);
            var isHoliday = !string.IsNullOrWhiteSpace(holidayName);
            var (events, moreText) = FitEvents(_events.GetValueOrDefault(DateOnly.FromDateTime(date)) ?? [], isHoliday);
            Days.Add(new CalendarDay
            {
                Date = date,
                IsPreviousMonth = monthStart < first,
                IsNextMonth = monthStart > first,
                IsToday = date.Date == _today,
                IsSunday = date.DayOfWeek == DayOfWeek.Sunday,
                IsSaturday = date.DayOfWeek == DayOfWeek.Saturday,
                IsHoliday = isHoliday,
                DayText = offset == 0 || date.Day == 1 ? $"{date.Month}월 {date.Day}" : date.Day.ToString(),
                HolidayName = holidayName ?? "",
                Events = events,
                MoreText = moreText
            });
        }
    }

    private (IReadOnlyList<DayEvent> Events, string MoreText) FitEvents(List<DayEvent> events, bool hasHoliday)
    {
        if (_cellWidth <= 0 || events.Count == 0)
        {
            return ([], "");
        }

        var available = _cellHeight - CellVerticalChrome - DateLineHeight - (hasHoliday ? HolidayLineHeight : 0);
        var shown = new List<DayEvent>();
        for (var index = 0; index < events.Count; index++)
        {
            var height = MeasureEvent(events[index]);
            var reserve = index == events.Count - 1 ? 0 : MoreLineHeight;
            if (shown.Count > 0 && height + reserve > available)
            {
                break;
            }

            shown.Add(events[index]);
            available -= height;
        }

        return shown.Count == events.Count ? (events, "") : (shown, $"+{events.Count - shown.Count}");
    }

    private double MeasureEvent(DayEvent calendarEvent)
    {
        var width = Math.Max(1, _cellWidth - CellHorizontalChrome - (calendarEvent.IsAllDay ? 0 : EventDotWidth));
        var text = new FormattedText(
            calendarEvent.Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            EventFontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = width,
            LineHeight = EventLineHeight
        };
        return text.Height + EventChromeHeight;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplySavedBounds()
    {
        ClampSettingsToVirtualScreen();

        Left = SystemParameters.VirtualScreenLeft + _settings.WindowLeft;
        Top = SystemParameters.VirtualScreenTop + _settings.WindowTop;
        MinWidth = 700;
        MinHeight = 420;
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
    }

    private void SaveCurrentBounds()
    {
        if (_suppressBoundsSave || !IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        _settings.WindowLeft = Math.Max(0, Left - SystemParameters.VirtualScreenLeft);
        _settings.WindowTop = Math.Max(0, Top - SystemParameters.VirtualScreenTop);
        _settings.WindowWidth = Math.Max(MinWidth, Width);
        _settings.WindowHeight = Math.Max(MinHeight, Height);
    }

    private void ClampSettingsToVirtualScreen()
    {
        var maxLeft = Math.Max(0, SystemParameters.VirtualScreenWidth - _settings.WindowWidth);
        var maxTop = Math.Max(0, SystemParameters.VirtualScreenHeight - _settings.WindowHeight);

        if (_settings.WindowWidth > SystemParameters.VirtualScreenWidth)
        {
            _settings.WindowWidth = SystemParameters.VirtualScreenWidth;
        }

        if (_settings.WindowHeight > SystemParameters.VirtualScreenHeight)
        {
            _settings.WindowHeight = SystemParameters.VirtualScreenHeight;
        }

        _settings.WindowLeft = Math.Min(Math.Max(_settings.WindowLeft, 0), maxLeft);
        _settings.WindowTop = Math.Min(Math.Max(_settings.WindowTop, 0), maxTop);
    }

    private void AttachToDesktop()
    {
        ShowInTaskbar = false;
        Topmost = false;
        _suppressBoundsSave = true;
        ApplySavedBounds();
        WindowBackdropService.EnableAcrylic(this);
        _desktopHost.Attach(this);
        _ = Dispatcher.BeginInvoke(() =>
        {
            ApplySavedBounds();
            WindowBackdropService.EnableAcrylic(this);
            _desktopHost.Repair(this);
            _suppressBoundsSave = false;
        }, DispatcherPriority.ApplicationIdle);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmMouseActivate)
        {
            var mouseMessage = GetSignedHighWord(lParam);
            handled = true;
            return mouseMessage == WmLeftButtonDoubleClick ? MaActivate : MaNoActivate;
        }

        if (msg != WmNcHitTest || WindowState != WindowState.Normal || _isDraggingShell)
        {
            return nint.Zero;
        }

        if (!GetWindowRect(hwnd, out var rect))
        {
            return nint.Zero;
        }

        var x = GetSignedLowWord(lParam);
        var y = GetSignedHighWord(lParam);
        var onLeft = x >= rect.Left && x < rect.Left + ResizeBorderPixels;
        var onRight = x <= rect.Right && x > rect.Right - ResizeBorderPixels;
        var onTop = y >= rect.Top && y < rect.Top + ResizeBorderPixels;
        var onBottom = y <= rect.Bottom && y > rect.Bottom - ResizeBorderPixels;

        var hit = (onLeft, onRight, onTop, onBottom) switch
        {
            (true, _, true, _) => HtTopLeft,
            (_, true, true, _) => HtTopRight,
            (true, _, _, true) => HtBottomLeft,
            (_, true, _, true) => HtBottomRight,
            (true, _, _, _) => HtLeft,
            (_, true, _, _) => HtRight,
            (_, _, true, _) => HtTop,
            (_, _, _, true) => HtBottom,
            _ => 0
        };

        if (hit == 0)
        {
            return nint.Zero;
        }

        handled = true;
        return hit;
    }

    private static int GetSignedLowWord(nint value)
    {
        return unchecked((short)((long)value & 0xFFFF));
    }

    private static int GetSignedHighWord(nint value)
    {
        return unchecked((short)(((long)value >> 16) & 0xFFFF));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(nint hWnd, out Rect rect);
}
