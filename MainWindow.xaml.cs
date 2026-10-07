using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
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

    private readonly SettingsService _settingsService = new();
    private readonly DriveMemoSyncService _syncService = new();
    private readonly KoreanHolidayService _holidayService = new();
    private readonly DesktopHostService _desktopHost = new();
    private readonly DispatcherTimer _desktopRepairTimer = new();

    private AppSettings _settings;
    private Dictionary<string, CalendarMemo> _memos;
    private DateTime _currentMonth;
    private CalendarDay? _editingDay;
    private string _editingOriginalText = "";
    private bool _isSavingEditor;
    private bool _suppressBoundsSave;
    private bool _isDraggingShell;
    private Point _dragStartScreen;
    private double _dragStartLeft;
    private double _dragStartTop;

    public MainWindow()
    {
        _settings = _settingsService.Load();
        _memos = _syncService.LoadLocal();
        _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        DataContext = this;
        InitializeComponent();

        ApplySavedBounds();
        BuildDays();
        ConfigureTimers();
    }

    public ObservableCollection<CalendarDay> Days { get; } = [];

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await SyncAndRenderAsync();
        _ = Dispatcher.BeginInvoke(AttachToDesktop, DispatcherPriority.ApplicationIdle);
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
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        await CommitActiveEditorAsync();
        _currentMonth = _currentMonth.AddMonths(-1);
        BuildDays();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        await CommitActiveEditorAsync();
        _currentMonth = _currentMonth.AddMonths(1);
        BuildDays();
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        await CommitActiveEditorAsync();
        await SyncAndRenderAsync();
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

    private async void DayCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: CalendarDay day })
        {
            return;
        }

        await CommitActiveEditorAsync();
        _editingDay = day;
        _editingOriginalText = day.Text;
        day.IsEditing = true;
        e.Handled = true;
    }

    private void MemoEditor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is CalendarDay { IsEditing: true })
        {
            textBox.Dispatcher.BeginInvoke(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }, DispatcherPriority.Input);
        }
    }

    private async void MemoEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: CalendarDay day } && day.IsEditing)
        {
            await CommitEditorAsync(day);
        }
    }

    private async void MemoEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: CalendarDay day })
        {
            return;
        }

        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            await CommitEditorAsync(day);
            Keyboard.ClearFocus();
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            day.Text = _editingOriginalText;
            day.IsEditing = false;
            _editingDay = null;
            Keyboard.ClearFocus();
        }
    }

    private async Task CommitActiveEditorAsync()
    {
        if (_editingDay is not null)
        {
            await CommitEditorAsync(_editingDay);
        }
    }

    private async Task CommitEditorAsync(CalendarDay day)
    {
        if (_isSavingEditor)
        {
            return;
        }

        _isSavingEditor = true;
        try
        {
            day.IsEditing = false;
            _editingDay = null;

            _memos.TryGetValue(day.Key, out var previous);
            var memo = _syncService.CreateMemo(day.Text, previous);
            _memos[day.Key] = memo;
            _syncService.SaveLocal(_memos);
            await SyncAndRenderAsync();
        }
        finally
        {
            _isSavingEditor = false;
        }
    }

    private async Task SyncAndRenderAsync()
    {
        try
        {
            _memos = await _syncService.SyncAsync(_settings, _memos);
        }
        catch
        {
            _syncService.SaveLocal(_memos);
        }

        BuildDays();
    }

    private void BuildDays()
    {
        var first = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
        var start = first.AddDays(-(int)first.DayOfWeek);
        var holidays = _holidayService.GetHolidays(start, start.AddDays(41));

        Days.Clear();
        for (var offset = 0; offset < 42; offset++)
        {
            var date = start.AddDays(offset);
            var key = date.ToString("yyyy-MM-dd");
            var monthStart = new DateTime(date.Year, date.Month, 1);
            _memos.TryGetValue(key, out var memo);
            holidays.TryGetValue(date.Date, out var holidayName);
            Days.Add(new CalendarDay
            {
                Date = date,
                IsPreviousMonth = monthStart < first,
                IsNextMonth = monthStart > first,
                IsToday = date.Date == DateTime.Today,
                IsSunday = date.DayOfWeek == DayOfWeek.Sunday,
                IsSaturday = date.DayOfWeek == DayOfWeek.Saturday,
                IsHoliday = !string.IsNullOrWhiteSpace(holidayName),
                DayText = offset == 0 || date.Day == 1 ? $"{date.Month}월 {date.Day}" : date.Day.ToString(),
                HolidayName = holidayName ?? "",
                Text = memo is { Deleted: false } ? memo.Text : ""
            });
        }
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
