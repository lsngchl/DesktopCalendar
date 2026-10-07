using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using DesktopCalendar.Models;

namespace DesktopCalendar.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AppSettings Load()
    {
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.SettingsPath))
        {
            var defaults = new AppSettings();
            defaults.Normalize();
            Save(defaults);
            return defaults;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(AppPaths.SettingsPath),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                }) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            var defaults = new AppSettings();
            defaults.Normalize();
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        AppPaths.Ensure();
        settings.Normalize();
        File.WriteAllText(AppPaths.SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
