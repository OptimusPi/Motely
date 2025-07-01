using System;
using System.IO;
using System.Text.Json;
using TheFool.Models;

namespace TheFool.Services;

public class ConfigService
{
    private readonly string _configPath;
    private AppSettings _settings = null!;

    public AppSettings Settings => _settings;
    public SearchCriteria CurrentCriteria => _settings.DefaultSearchCriteria;
    public string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheFool");

    public ConfigService()
    {
        Directory.CreateDirectory(DataDirectory);
        _configPath = Path.Combine(DataDirectory, "settings.json");
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                _settings = new AppSettings();
                SaveSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_configPath, json);
        }
        catch
        {
            // Log but don't crash
        }
    }

    public void UpdateSearchCriteria(SearchCriteria criteria)
    {
        _settings.DefaultSearchCriteria = criteria;
        SaveSettings();
    }
}

public class AppSettings
{
    public WindowSettings Window { get; set; } = new();
    public SearchCriteria DefaultSearchCriteria { get; set; } = new();
    public string OuijaPath { get; set; } = "ouija-cli.exe";
    public bool EnableLogging { get; set; } = true;
    public string Theme { get; set; } = "Light";
}

public class WindowSettings
{
    public int Width { get; set; } = 1600;
    public int Height { get; set; } = 900;
    public bool Maximized { get; set; }
}
