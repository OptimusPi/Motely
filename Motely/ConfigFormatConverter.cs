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
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<MotelyJsonConfig>(jamlContent);
            config?.PostProcess();
            return config;
        }
        catch
        {
            return null;
        }
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
    /// </summary>
    public static string SaveAsJaml(this MotelyJsonConfig config)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        return serializer.Serialize(config);
    }

    #endregion
}
