using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using DesktopCalendar.Models;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace DesktopCalendar.Services;

public sealed class CalendarFeedService
{
    public const string KoreanHolidayFeedUrl =
        "https://calendar.google.com/calendar/ical/ko.south_korea%23holiday%40group.v.calendar.google.com/public/basic.ics";

    private const string CalendarsTemplate = """
        // Google 캘린더 설정 > 캘린더 통합 > "iCal 형식의 비공개 주소"를 캘린더마다 하나씩 넣는다.
        // Color는 그 캘린더 일정에 쓰는 색이다.
        [
          // { "Url": "https://calendar.google.com/calendar/ical/.../basic.ics", "Color": "#8AB4F8" }
        ]

        """;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly Dictionary<string, Calendar> _feeds = new(StringComparer.Ordinal);
    private IReadOnlyList<CalendarSource> _sources = [];

    public bool HasSources => _sources.Count > 0;
    public Calendar? Holidays => _feeds.GetValueOrDefault(KoreanHolidayFeedUrl);

    public void LoadCache()
    {
        TryLoadSources();
        foreach (var url in FeedUrls())
        {
            try
            {
                var path = CachePath(url);
                if (File.Exists(path))
                {
                    _feeds[url] = Parse(File.ReadAllText(path, Encoding.UTF8));
                }
            }
            catch (Exception)
            {
                // A broken cache file is replaced on the next successful refresh.
            }
        }
    }

    public async Task<bool> RefreshAsync()
    {
        var succeeded = TryLoadSources();
        var urls = FeedUrls().ToList();

        foreach (var url in urls)
        {
            try
            {
                var text = await Http.GetStringAsync(url);
                _feeds[url] = await Task.Run(() => Parse(text));
                await WriteCacheAsync(url, text);
            }
            catch (Exception)
            {
                succeeded = false;
            }
        }

        foreach (var url in _feeds.Keys.Except(urls).ToList())
        {
            _feeds.Remove(url);
        }

        return succeeded;
    }

    public Dictionary<DateOnly, List<DayEvent>> GetEvents(DateOnly first, DateOnly last)
    {
        var result = new Dictionary<DateOnly, List<DayEvent>>();
        var rangeStart = new CalDateTime(first);
        var rangeEnd = new CalDateTime(last.AddDays(1));

        foreach (var source in _sources)
        {
            if (!_feeds.TryGetValue(source.Url, out var calendar))
            {
                continue;
            }

            var color = ParseBrush(source.Color);
            var background = WithAlpha(color, 0x55);
            try
            {
                foreach (var occurrence in calendar.GetOccurrences(rangeStart).TakeWhileBefore(rangeEnd))
                {
                    if (occurrence.Source is not CalendarEvent calendarEvent
                        || string.Equals(calendarEvent.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    AddOccurrence(result, occurrence.Period, calendarEvent.Summary ?? "", color, background, first, last);
                }
            }
            catch (Exception)
            {
                // One malformed calendar must not hide the others.
            }
        }

        foreach (var events in result.Values)
        {
            events.Sort((left, right) =>
                left.IsAllDay != right.IsAllDay ? (left.IsAllDay ? -1 : 1)
                : left.Start != right.Start ? left.Start.CompareTo(right.Start)
                : string.CompareOrdinal(left.Title, right.Title));
        }

        return result;
    }

    private static void AddOccurrence(
        Dictionary<DateOnly, List<DayEvent>> result,
        Period period,
        string title,
        SolidColorBrush color,
        SolidColorBrush background,
        DateOnly first,
        DateOnly last)
    {
        var start = period.StartTime;
        var end = period.EffectiveEndTime ?? start;
        var isAllDay = !start.HasTime;

        DateTime startLocal;
        DateOnly lastDay;
        if (isAllDay)
        {
            startLocal = start.Date.ToDateTime(TimeOnly.MinValue);
            var endDate = end.HasTime ? end.Date.AddDays(1) : end.Date;
            lastDay = endDate > start.Date ? endDate.AddDays(-1) : start.Date;
        }
        else
        {
            startLocal = ToLocal(start);
            var endLocal = ToLocal(end);
            lastDay = DateOnly.FromDateTime(endLocal > startLocal ? endLocal.AddTicks(-1) : startLocal);
        }

        var startDay = DateOnly.FromDateTime(startLocal);
        for (var day = startDay < first ? first : startDay; day <= lastDay && day <= last; day = day.AddDays(1))
        {
            if (!result.TryGetValue(day, out var events))
            {
                events = [];
                result[day] = events;
            }

            events.Add(new DayEvent
            {
                Time = isAllDay || day != startDay ? "" : $"{startLocal:HH:mm} ",
                Title = title,
                IsAllDay = isAllDay,
                Start = startLocal,
                Color = color,
                Background = isAllDay ? background : Brushes.Transparent
            });
        }
    }

    private static DateTime ToLocal(CalDateTime value)
    {
        return value.IsFloating ? value.Value : value.AsUtc.ToLocalTime();
    }

    private bool TryLoadSources()
    {
        try
        {
            AppPaths.Ensure();
            if (!File.Exists(AppPaths.CalendarsPath))
            {
                File.WriteAllText(AppPaths.CalendarsPath, CalendarsTemplate, new UTF8Encoding(false));
            }

            var sources = JsonSerializer.Deserialize<List<CalendarSource>>(
                File.ReadAllText(AppPaths.CalendarsPath, Encoding.UTF8),
                ReadOptions) ?? [];
            _sources = sources.Where(source => !string.IsNullOrWhiteSpace(source.Url)).ToList();
            return true;
        }
        catch (Exception)
        {
            // Keep the last good list so a typo in calendars.json does not blank the calendar.
            return false;
        }
    }

    private IEnumerable<string> FeedUrls()
    {
        return _sources.Select(source => source.Url).Append(KoreanHolidayFeedUrl).Distinct(StringComparer.Ordinal);
    }

    private static Calendar Parse(string text)
    {
        return Calendar.Load(text) ?? throw new InvalidDataException("Empty calendar feed.");
    }

    private static async Task WriteCacheAsync(string url, string text)
    {
        Directory.CreateDirectory(AppPaths.FeedCacheRoot);
        var path = CachePath(url);
        var tempPath = $"{path}.tmp";
        await File.WriteAllTextAsync(tempPath, text, new UTF8Encoding(false));
        File.Move(tempPath, path, overwrite: true);
    }

    private static string CachePath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        return Path.Combine(AppPaths.FeedCacheRoot, $"{hash}.ics");
    }

    private static SolidColorBrush ParseBrush(string value)
    {
        var color = Color.FromRgb(0x8A, 0xB4, 0xF8);
        try
        {
            if (ColorConverter.ConvertFromString(value) is Color parsed)
            {
                color = parsed;
            }
        }
        catch (FormatException)
        {
            // Fall back to the default color for an unreadable value.
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush WithAlpha(SolidColorBrush brush, byte alpha)
    {
        var color = brush.Color;
        var result = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
        result.Freeze();
        return result;
    }
}
