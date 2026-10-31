using System.Text.Json;
using System.IO;

namespace UmiClassGuardLite.Core;

public static class SettingsStore
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UmiClassGuardLite");
    public static string SettingsPath => Path.Combine(DataDirectory, "config.json");
    public static string LogPath => Path.Combine(DataDirectory, "activity.jsonl");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static GuardSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<GuardSettings>(File.ReadAllText(SettingsPath), Options) ?? new();
        }
        catch (Exception ex)
        {
            Diagnostics.Record("CG-1002", "Settings.Load", "Cấu hình hỏng hoặc không đọc được; dùng mặc định.", ex);
        }
        return new();
    }

    public static void Save(GuardSettings value)
    {
        Directory.CreateDirectory(DataDirectory);
        string temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, SettingsPath, true);
    }

    public static void AddActivity(ActivityEntry entry)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.AppendAllText(LogPath, JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        catch (IOException ex) { Diagnostics.Record("CG-1004", "Activity.Write", "Không thể ghi nhật ký hoạt động.", ex); }
        catch (UnauthorizedAccessException ex) { Diagnostics.Record("CG-1004", "Activity.Write", "Không có quyền ghi nhật ký hoạt động.", ex); }
    }

    public static List<ActivityEntry> ReadActivity(int max = 100)
    {
        if (!File.Exists(LogPath)) return [];
        try
        {
            return File.ReadLines(LogPath)
                .TakeLast(max)
                .Select(x => { try { return JsonSerializer.Deserialize<ActivityEntry>(x); } catch { return null; } })
                .Where(x => x is not null).Cast<ActivityEntry>().Reverse().ToList();
        }
        catch (Exception) { return []; }
    }
}
