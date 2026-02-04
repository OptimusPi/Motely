using System.Text.Json;
using System.Text.Json.Serialization;
using Motely.Filters;

namespace Motely;

/// <summary>
/// Provides format conversion capabilities for MotelyJsonConfig
/// Enables round-trip conversion between JSON and JAML formats
/// JAML (Joker Ante Markup Language) is a YAML-based format for Balatro filters
/// </summary>
public static class ConfigFormatConverter
{
    #region Load Methods

    /// <summary>
    /// Load config from JSON string (AOT-compatible via source generation)
    /// </summary>
    public static MotelyJsonConfig? LoadFromJsonString(string jsonContent)
    {
        try
        {
            // Use AOT-compatible source-generated serializer
            var config = JsonSerializer.Deserialize(
                jsonContent,
                MotelyJsonSerializerContext.Default.MotelyJsonConfig
            );
            config?.PostProcess();

            // Validate config just like JAML loader does
            if (config is not null)
            {
                MotelyJsonConfigValidator.ValidateConfig(config);
            }

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LoadFromJsonString error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load config from JAML string (Joker Ante Markup Language - YAML-based)
    /// </summary>
    public static MotelyJsonConfig? LoadFromJamlString(string jamlContent)
    {
        if (JamlConfigLoader.TryLoadFromJamlString(jamlContent, out var config, out var error))
        {
            return config;
        }

        Console.WriteLine($"LoadFromJamlString error: {error}");
        return null;
    }

    #endregion

    #region Save Methods

    /// <summary>
    /// Save config to JSON string (AOT-compatible via source generation)
    /// </summary>
    public static string SaveAsJson(this MotelyJsonConfig config)
    {
        return JsonSerializer.Serialize(
            config,
            MotelyJsonSerializerContext.Default.MotelyJsonConfig
        );
    }

    /// <summary>
    /// Save config to JAML string (Joker Ante Markup Language)
    /// Uses JamlFormatter for clean, idiomatic output:
    /// - type-as-key format: "joker: Blueprint"
    /// - compact numeric arrays: "antes: [1,2,3]"
    /// - omits null/empty/default properties
    /// </summary>
    public static string SaveAsJaml(this MotelyJsonConfig config)
    {
        return JamlFormatter.Format(config);
    }

    #endregion
}
