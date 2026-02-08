using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motely.TUI;

/// <summary>
/// Runtime settings for TUI searches and API server
/// </summary>
public static class TuiSettings
{
    private static readonly SettingsService _settingsService = new SettingsService("tui.json");

    // Thread settings
    public static int ThreadCount { get; set; } = Environment.ProcessorCount;

    // Batch settings
    public static int BatchCharacterCount { get; set; } = 2;

    // API Server settings
    public static string ApiServerHost { get; set; } = "localhost";
    public static int ApiServerPort { get; set; } = 3141;

    // Secret settings (in-memory only, not persisted)
    public static bool CrudeSeedsEnabled { get; set; } = false;

    /// <summary>
    /// Load settings from tui.json (if exists)
    /// </summary>
    public static void Load() => _settingsService.LoadSettings();

    /// <summary>
    /// Save settings to tui.json
    /// </summary>
    public static void Save() => _settingsService.SaveSettings();

    /// <summary>
    /// Reset all settings to defaults
    /// </summary>
    public static void ResetToDefaults()
    {
        ThreadCount = Environment.ProcessorCount;
        BatchCharacterCount = 2;
        ApiServerHost = "localhost";
        ApiServerPort = 3141;
        Save();
    }
}

public class SettingsService
{
    private readonly string _fileName;

    public SettingsService(string fileName)
    {
        _fileName = fileName;
    }

    public void LoadSettings()
    {
        try
        {
            if (string.IsNullOrEmpty(_fileName) || !File.Exists(_fileName))
                return;

            var json = File.ReadAllText(_fileName);
            var settings = JsonSerializer.Deserialize<PersistedSettings>(json);

            if (settings != null)
            {
                TuiSettings.ThreadCount = settings.ThreadCount ?? Environment.ProcessorCount;
                TuiSettings.BatchCharacterCount = settings.BatchCharacterCount ?? 2;
                TuiSettings.ApiServerHost = settings.ApiServerHost ?? "localhost";
                TuiSettings.ApiServerPort = settings.ApiServerPort ?? 3141;
            }
        }
        catch (Exception)
        {
            // If load fails, just use defaults; never throw
        }
    }

    public void SaveSettings()
    {
        try
        {
            var settings = new PersistedSettings
            {
                ThreadCount = TuiSettings.ThreadCount,
                BatchCharacterCount = TuiSettings.BatchCharacterCount,
                ApiServerHost = TuiSettings.ApiServerHost,
                ApiServerPort = TuiSettings.ApiServerPort,
            };

            var options = new JsonSerializerOptions { WriteIndented = true };

            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_fileName, json);
        }
        catch
        {
            // Silently fail if save fails
        }
    }

    private class PersistedSettings
    {
        public int? ThreadCount { get; set; }
        public int? BatchCharacterCount { get; set; }
        public string? ApiServerHost { get; set; }
        public int? ApiServerPort { get; set; }
    }
}
