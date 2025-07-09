using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motely.Filters;

/// <summary>
/// Simple config loader that maintains backward compatibility while providing a clean API
/// </summary>
public static class OuijaConfigLoader
{
    /// <summary>
    /// Load config from file with multiple path attempts
    /// </summary>
    public static OuijaConfig Load(string configName)
    {
        string[] searchPaths =
        {
            Path.Combine(AppContext.BaseDirectory, "JsonItemFilters", configName),
            Path.Combine(AppContext.BaseDirectory, "JsonItemFilters", configName + ".json"),
            Path.Combine(AppContext.BaseDirectory, "JsonItemFilters", configName + ".ouija.json"),
            Path.Combine(".", "JsonItemFilters", configName),
            Path.Combine(".", "JsonItemFilters", configName + ".json"),
            Path.Combine(".", "JsonItemFilters", configName + ".ouija.json"),
            configName,
            configName + ".json",
            configName + ".ouija.json"
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"Loading config from: {path}");
                return OuijaConfig.LoadFromJson(path);
            }
        }

        throw new FileNotFoundException($"Could not find Ouija config: {configName}");
    }

    /// <summary>
    /// Save config to JSON file
    /// </summary>
    public static void Save(OuijaConfig config, string path)
    {
        var json = config.ToJson();
        File.WriteAllText(path, json);
        Console.WriteLine($"Saved config to: {path}");
    }

    /// <summary>
    /// Create and save an example config
    /// </summary>
    public static void CreateExampleConfig(string path = "example.ouija.json")
    {
        var example = OuijaConfig.CreateExample();
        Save(example, path);
    }
}
