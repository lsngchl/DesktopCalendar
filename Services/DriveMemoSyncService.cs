using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DesktopCalendar.Models;

namespace DesktopCalendar.Services;

public sealed class DriveMemoSyncService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public Dictionary<string, CalendarMemo> LoadLocal()
    {
        AppPaths.Ensure();
        return ReadStore(AppPaths.LocalStorePath).Events;
    }

    public void SaveLocal(Dictionary<string, CalendarMemo> memos)
    {
        AppPaths.Ensure();
        WriteStore(AppPaths.LocalStorePath, new DriveMemoStore { Events = Normalize(memos) });
    }

    public async Task<Dictionary<string, CalendarMemo>> SyncAsync(AppSettings settings, Dictionary<string, CalendarMemo> local)
    {
        var localChanged = false;
        var remotePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(settings.SyncPath));
        var remoteExists = File.Exists(remotePath);
        var remoteStore = remoteExists ? await ReadStoreAsync(remotePath) : new DriveMemoStore();
        var remoteChanged = !remoteExists;

        remoteStore.Events = Normalize(remoteStore.Events);
        foreach (var pair in remoteStore.Events.ToArray())
        {
            if (!IsDateKey(pair.Key))
            {
                remoteStore.Events.Remove(pair.Key);
                remoteChanged = true;
                continue;
            }

            NormalizeMemo(pair.Value);
            if (local.TryGetValue(pair.Key, out var localMemo))
            {
                var comparison = Compare(pair.Value, localMemo);
                if (comparison > 0)
                {
                    local[pair.Key] = Clone(pair.Value);
                    localChanged = true;
                }
                else if (comparison < 0)
                {
                    remoteStore.Events[pair.Key] = Clone(localMemo);
                    remoteChanged = true;
                }
            }
            else
            {
                local[pair.Key] = Clone(pair.Value);
                localChanged = true;
            }
        }

        foreach (var pair in local.ToArray())
        {
            if (!IsDateKey(pair.Key))
            {
                local.Remove(pair.Key);
                localChanged = true;
                continue;
            }

            NormalizeMemo(pair.Value);
            if (!remoteStore.Events.TryGetValue(pair.Key, out var remoteMemo) || Compare(remoteMemo, pair.Value) < 0)
            {
                remoteStore.Events[pair.Key] = Clone(pair.Value);
                remoteChanged = true;
            }
        }

        if (remoteChanged)
        {
            await WriteStoreAsync(remotePath, remoteStore);
        }

        if (localChanged || remoteChanged)
        {
            SaveLocal(local);
        }

        return Normalize(local);
    }

    public CalendarMemo CreateMemo(string text, CalendarMemo? previous)
    {
        var now = DateTimeOffset.UtcNow;
        var previousTimestamp = previous is null ? DateTimeOffset.MinValue : ParseTimestamp(previous.UpdatedAtUtc);
        if (previousTimestamp >= now)
        {
            now = previousTimestamp.AddMilliseconds(1);
        }

        return new CalendarMemo
        {
            Text = text.Trim(),
            Deleted = string.IsNullOrWhiteSpace(text),
            UpdatedAtUtc = FormatTimestamp(now)
        };
    }

    public static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    private static DriveMemoStore ReadStore(string path)
    {
        if (!File.Exists(path))
        {
            return new DriveMemoStore();
        }

        try
        {
            return JsonSerializer.Deserialize<DriveMemoStore>(File.ReadAllText(path, Encoding.UTF8), ReadOptions) ?? new DriveMemoStore();
        }
        catch
        {
            return new DriveMemoStore();
        }
    }

    private static async Task<DriveMemoStore> ReadStoreAsync(string path)
    {
        if (!File.Exists(path))
        {
            return new DriveMemoStore();
        }

        try
        {
            return JsonSerializer.Deserialize<DriveMemoStore>(await File.ReadAllTextAsync(path, Encoding.UTF8), ReadOptions) ?? new DriveMemoStore();
        }
        catch
        {
            return new DriveMemoStore();
        }
    }

    private static void WriteStore(string path, DriveMemoStore store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        store.Events = Normalize(store.Events);
        File.WriteAllText(path, JsonSerializer.Serialize(store, WriteOptions), new UTF8Encoding(false));
    }

    private static async Task WriteStoreAsync(string path, DriveMemoStore store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        store.Events = Normalize(store.Events);

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(store, WriteOptions), new UTF8Encoding(false));
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static Dictionary<string, CalendarMemo> Normalize(Dictionary<string, CalendarMemo>? memos)
    {
        return (memos ?? [])
            .Where(pair => IsDateKey(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair =>
            {
                var memo = Clone(pair.Value);
                NormalizeMemo(memo);
                return memo;
            }, StringComparer.Ordinal);
    }

    private static void NormalizeMemo(CalendarMemo memo)
    {
        memo.Text ??= "";
        memo.UpdatedAtUtc = string.IsNullOrWhiteSpace(memo.UpdatedAtUtc)
            ? FormatTimestamp(DateTimeOffset.UtcNow)
            : FormatTimestamp(ParseTimestamp(memo.UpdatedAtUtc));
    }

    private static CalendarMemo Clone(CalendarMemo memo)
    {
        return new CalendarMemo
        {
            Text = memo.Text ?? "",
            Deleted = memo.Deleted,
            UpdatedAtUtc = memo.UpdatedAtUtc ?? ""
        };
    }

    private static int Compare(CalendarMemo left, CalendarMemo right)
    {
        return ParseTimestamp(left.UpdatedAtUtc).CompareTo(ParseTimestamp(right.UpdatedAtUtc));
    }

    private static DateTimeOffset ParseTimestamp(string? value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return DateTimeOffset.MinValue;
    }

    private static bool IsDateKey(string value)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
