using System.IO;

namespace DesktopCalendar.Models;

public sealed class AppSettings
{
    public string SyncPath { get; set; } = "";
    public double WindowLeft { get; set; } = 120;
    public double WindowTop { get; set; } = 35;
    public double WindowWidth { get; set; } = 1421;
    public double WindowHeight { get; set; } = 684;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(SyncPath))
        {
            SyncPath = DefaultSyncPath();
        }

        WindowWidth = Math.Clamp(WindowWidth, 700, 2500);
        WindowHeight = Math.Clamp(WindowHeight, 420, 1400);
    }

    private static string DefaultSyncPath()
    {
        var candidates = new[]
        {
            @"G:\내 드라이브",
            @"G:\My Drive",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Google Drive"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "My Drive")
        };

        var root = candidates.FirstOrDefault(path =>
        {
            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }) ?? @"G:\내 드라이브";

        return Path.Combine(root, "Apps", "DesktopCalendar", "events.json");
    }
}
