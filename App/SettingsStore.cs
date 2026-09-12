using System.Text.Json;
using System.IO;

namespace HardwareMonitor;

internal static class SettingsStore
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HardwareMonitor");

    public static T? Read<T>(string path, out string? notice) where T : class
    {
        notice = null;
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            try
            {
                using var stream = File.OpenRead(candidate);
                var value = JsonSerializer.Deserialize<T>(stream) ?? throw new JsonException("Empty settings document.");
                if (candidate != path)
                    notice = "Settings recovered from backup";
                return value;
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                notice = "Some saved settings could not be read";
            }
        }
        return null;
    }

    // Complete the new file on disk before replacing the previous document.
    // A unique temporary file also avoids collisions with another writer.
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path))
                File.Replace(temporary, path, path + ".bak");
            else
                File.Move(temporary, path);
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

internal sealed record FpsSettings(bool Enabled = true, string? Application = null, bool Show = true,
    bool ShowLive = true, bool ShowAverage = true, bool ShowLow = true, int ResetSeconds = 0);
internal sealed record ReadingPlacement(string Title, int Column);

internal sealed record DashboardPreferences
{
    public Dictionary<string, bool> Visibility { get; init; } = [];
    public Dictionary<string, double> Thresholds { get; init; } = [];
    public List<ReadingPlacement> Arrangement { get; init; } = [];
    public FpsSettings Fps { get; init; } = new();
    public Dictionary<string, string> SensorOverrides { get; init; } = [];

    public static Dictionary<string, bool> MergeVisibility(IReadOnlyDictionary<string, bool> saved, IReadOnlyDictionary<string, bool> initial, IReadOnlyDictionary<string, bool> edited)
    {
        var result = new Dictionary<string, bool>(saved);
        foreach (var pair in edited)
            if (!initial.TryGetValue(pair.Key, out var previous) || pair.Value != previous) result[pair.Key] = pair.Value;
        return result;
    }

    public static string FilePath => Path.Combine(SettingsStore.Folder, "dashboard.json");

    public static DashboardPreferences Load(out string? notice, string? folder = null)
    {
        folder ??= SettingsStore.Folder;
        var saved = SettingsStore.Read<DashboardPreferences>(Path.Combine(folder, "dashboard.json"), out notice);
        if (saved == null)
        {
            // Read the prototype's separate files without changing or deleting them.
            T? Legacy<T>(string file) where T : class => SettingsStore.Read<T>(Path.Combine(folder, file), out _);
            saved = new()
            {
                Visibility = Legacy<Dictionary<string, bool>>("sensors.json") ?? [],
                Thresholds = Legacy<Dictionary<string, double>>("temperature-warnings.json") ?? [],
                Arrangement = Legacy<List<ReadingPlacement>>("layout.json") ?? [],
                Fps = Legacy<FpsSettings>("fps.json") ?? new()
            };
        }
        return saved with
        {
            SensorOverrides = (saved.SensorOverrides ?? []).Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value)).ToDictionary(),
            Visibility = saved.Visibility ?? [],
            Thresholds = (saved.Thresholds ?? []).Where(p => double.IsFinite(p.Value) && p.Value >= 1 && p.Value <= 200).ToDictionary(),
            Arrangement = (saved.Arrangement ?? []).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Title) && p.Column is 0 or 1).DistinctBy(p => p.Title).ToList(),
            Fps = (saved.Fps ?? new()) with
            {
                Application = string.IsNullOrWhiteSpace(saved.Fps?.Application) ? null : saved.Fps.Application,
                ResetSeconds = saved.Fps?.ResetSeconds is 30 or 60 or 300 or 600 ? saved.Fps.ResetSeconds : 0
            }
        };
    }
}


