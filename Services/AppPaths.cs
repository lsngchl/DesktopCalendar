using System.IO;

namespace DesktopCalendar.Services;

public static class AppPaths
{
    public static string AppDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopCalendar");

    public static string SettingsPath => Path.Combine(AppDataRoot, "settings.json");
    public static string CalendarsPath => Path.Combine(AppDataRoot, "calendars.json");
    public static string FeedCacheRoot => Path.Combine(AppDataRoot, "cache");

    public static void Ensure()
    {
        Directory.CreateDirectory(AppDataRoot);
    }
}
