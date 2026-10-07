using System.Globalization;
using IcsCalendar = Ical.Net.Calendar;

namespace DesktopCalendar.Services;

public sealed class KoreanHolidayService
{
    private enum SubstituteRule
    {
        None,
        SaturdaySundayOrOverlap,
        SundayOrOverlap
    }

    private sealed record HolidayCandidate(DateTime Date, string Name, SubstituteRule SubstituteRule);

    private static readonly KoreanLunisolarCalendar LunarCalendar = new();
    private static readonly string[] FeedHolidayDescriptions = ["공휴일", "Public holiday"];

    // Rules cover the regular holidays offline; the Google feed adds the irregular ones such as election days.
    public IReadOnlyDictionary<DateTime, string> GetHolidays(DateTime start, DateTime end, IcsCalendar? feed)
    {
        start = start.Date;
        end = end.Date;

        var candidates = BuildCandidates(start.Year - 1, end.Year + 1);
        var regularByDate = candidates
            .GroupBy(candidate => candidate.Date.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        var namesByDate = regularByDate.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(candidate => candidate.Name).Distinct().ToList());
        var occupied = new HashSet<DateTime>(regularByDate.Keys);

        // Holidays that overlap on one date earn a single substitute day between them.
        foreach (var (date, sameDateCandidates) in regularByDate.OrderBy(pair => pair.Key))
        {
            if (!sameDateCandidates.Any(candidate => NeedsSubstituteHoliday(candidate, sameDateCandidates)))
            {
                continue;
            }

            var substituteDate = NextSubstituteDate(date, occupied);
            occupied.Add(substituteDate);
            AddName(namesByDate, substituteDate, "대체공휴일");
        }

        foreach (var (date, name) in FeedHolidays(feed))
        {
            if (!namesByDate.ContainsKey(date))
            {
                AddName(namesByDate, date, name);
            }
        }

        return namesByDate
            .Where(pair => pair.Key >= start && pair.Key <= end)
            .OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => string.Join(" · ", pair.Value));
    }

    private static IEnumerable<(DateTime Date, string Name)> FeedHolidays(IcsCalendar? feed)
    {
        if (feed is null)
        {
            yield break;
        }

        foreach (var holiday in feed.Events)
        {
            if (holiday.DtStart is { HasTime: false } start
                && FeedHolidayDescriptions.Contains(holiday.Description?.Trim(), StringComparer.Ordinal))
            {
                yield return (start.Date.ToDateTime(TimeOnly.MinValue), holiday.Summary ?? "");
            }
        }
    }

    private static List<HolidayCandidate> BuildCandidates(int startYear, int endYear)
    {
        var candidates = new List<HolidayCandidate>();
        for (var year = startYear; year <= endYear; year++)
        {
            AddSolarHolidays(candidates, year);
            AddLunarHolidays(candidates, year);
        }

        return candidates
            .GroupBy(candidate => new { candidate.Date, candidate.Name, candidate.SubstituteRule })
            .Select(group => group.First())
            .ToList();
    }

    private static void AddSolarHolidays(List<HolidayCandidate> candidates, int year)
    {
        Add(candidates, year, 1, 1, "신정", SubstituteRule.None);
        Add(candidates, year, 3, 1, "삼일절", SubstituteRule.SaturdaySundayOrOverlap);

        if (year >= 2026)
        {
            Add(candidates, year, 5, 1, "노동절", SubstituteRule.SaturdaySundayOrOverlap);
        }

        Add(candidates, year, 5, 5, "어린이날", SubstituteRule.SaturdaySundayOrOverlap);
        Add(candidates, year, 6, 6, "현충일", SubstituteRule.None);

        if (year >= 2026)
        {
            Add(candidates, year, 7, 17, "제헌절", SubstituteRule.SaturdaySundayOrOverlap);
        }

        Add(candidates, year, 8, 15, "광복절", SubstituteRule.SaturdaySundayOrOverlap);
        Add(candidates, year, 10, 3, "개천절", SubstituteRule.SaturdaySundayOrOverlap);
        Add(candidates, year, 10, 9, "한글날", SubstituteRule.SaturdaySundayOrOverlap);
        Add(candidates, year, 12, 25, "성탄절", SubstituteRule.SaturdaySundayOrOverlap);
    }

    private static void AddLunarHolidays(List<HolidayCandidate> candidates, int lunarYear)
    {
        try
        {
            var seollal = LunarToSolar(lunarYear, 1, 1);
            candidates.Add(new HolidayCandidate(seollal.AddDays(-1), "설날 연휴", SubstituteRule.SundayOrOverlap));
            candidates.Add(new HolidayCandidate(seollal, "설날", SubstituteRule.SundayOrOverlap));
            candidates.Add(new HolidayCandidate(seollal.AddDays(1), "설날 연휴", SubstituteRule.SundayOrOverlap));

            candidates.Add(new HolidayCandidate(LunarToSolar(lunarYear, 4, 8), "부처님오신날", SubstituteRule.SaturdaySundayOrOverlap));

            var chuseok = LunarToSolar(lunarYear, 8, 15);
            candidates.Add(new HolidayCandidate(chuseok.AddDays(-1), "추석 연휴", SubstituteRule.SundayOrOverlap));
            candidates.Add(new HolidayCandidate(chuseok, "추석", SubstituteRule.SundayOrOverlap));
            candidates.Add(new HolidayCandidate(chuseok.AddDays(1), "추석 연휴", SubstituteRule.SundayOrOverlap));
        }
        catch (ArgumentOutOfRangeException)
        {
            // The built-in Korean lunar calendar has a finite supported range.
        }
    }

    private static void Add(List<HolidayCandidate> candidates, int year, int month, int day, string name, SubstituteRule rule)
    {
        candidates.Add(new HolidayCandidate(new DateTime(year, month, day), name, rule));
    }

    private static DateTime LunarToSolar(int lunarYear, int lunarMonth, int lunarDay)
    {
        var calendarMonth = ToCalendarMonth(lunarYear, lunarMonth);
        return LunarCalendar.ToDateTime(lunarYear, calendarMonth, lunarDay, 0, 0, 0, 0);
    }

    private static int ToCalendarMonth(int lunarYear, int lunarMonth)
    {
        var leapMonth = LunarCalendar.GetLeapMonth(lunarYear);
        return leapMonth > 0 && leapMonth <= lunarMonth ? lunarMonth + 1 : lunarMonth;
    }

    private static bool NeedsSubstituteHoliday(HolidayCandidate candidate, IReadOnlyCollection<HolidayCandidate> sameDateCandidates)
    {
        var day = candidate.Date.DayOfWeek;
        var overlapsAnotherHoliday = sameDateCandidates.Count > 1 && day is not DayOfWeek.Saturday and not DayOfWeek.Sunday;

        return candidate.SubstituteRule switch
        {
            SubstituteRule.SaturdaySundayOrOverlap => day is DayOfWeek.Saturday or DayOfWeek.Sunday || overlapsAnotherHoliday,
            SubstituteRule.SundayOrOverlap => day == DayOfWeek.Sunday || overlapsAnotherHoliday,
            _ => false
        };
    }

    private static DateTime NextSubstituteDate(DateTime date, HashSet<DateTime> occupied)
    {
        var candidate = date.Date.AddDays(1);
        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || occupied.Contains(candidate))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    private static void AddName(Dictionary<DateTime, List<string>> namesByDate, DateTime date, string name)
    {
        if (!namesByDate.TryGetValue(date, out var names))
        {
            names = [];
            namesByDate[date] = names;
        }

        if (!names.Contains(name, StringComparer.Ordinal))
        {
            names.Add(name);
        }
    }
}
