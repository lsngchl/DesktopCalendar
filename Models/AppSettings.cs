namespace DesktopCalendar.Models;

public sealed class AppSettings
{
    public double WindowLeft { get; set; } = 120;
    public double WindowTop { get; set; } = 35;
    public double WindowWidth { get; set; } = 1421;
    public double WindowHeight { get; set; } = 684;

    public void Normalize()
    {
        WindowWidth = Math.Clamp(WindowWidth, 700, 2500);
        WindowHeight = Math.Clamp(WindowHeight, 420, 1400);
    }
}
