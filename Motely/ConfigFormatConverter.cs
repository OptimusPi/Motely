using System.Text.Json;
using System.Text.Json.Serialization;
using Motely.Filters;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Motely;

/// <summary>
/// Provides format conversion capabilities for MotelyJsonConfig
/// Enables round-trip conversion between JSON and JAML formats
/// JAML (Joker Ante Markup Language) is a YAML-based format for Balatro filters
/// </summary>
public static class ConfigFormatConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    #region Load Methods

    /// <summary>
    /// Load config from JSON string
    /// </summary>
    public static MotelyJsonConfig? LoadFromJsonString(string jsonContent)
    {
        try
        {
            // Use same options as the original TryLoadFromJsonFile
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                // Note: Not using UnmappedMemberHandling since we want to be lenient for format conversion
            };

            var config = JsonSerializer.Deserialize<MotelyJsonConfig>(jsonContent, options);
            config?.PostProcess();
            
            // Validate config just like JAML loader does
            if (config != null)
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
    /// Save config to JSON string
    /// </summary>
    public static string SaveAsJson(this MotelyJsonConfig config)
    {
        return JsonSerializer.Serialize(config, JsonOptions);
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
