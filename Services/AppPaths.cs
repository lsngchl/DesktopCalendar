using System.IO;

namespace DesktopCalendar.Services;

public static class AppPaths
{
    public static string AppDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopCalendar");

    public static string SettingsPath => Path.Combine(AppDataRoot, "settings.json");
    public static string LocalStorePath => Path.Combine(AppDataRoot, "local-events.json");

    public static void Ensure()
    {
        Directory.CreateDirectory(AppDataRoot);
    }
}
