using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Motely.Filters;
using Motely.Filters.MotelyJson;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Motely;

/// <summary>
/// JAML (Joker Ante Markup Language) configuration loader.
/// JAML is a YAML-based format specifically designed for Balatro seed filter configuration.
/// </summary>
public static class JamlConfigLoader
{
    /// <summary>
    /// Try to load a MotelyJsonConfig from a JAML file.
    /// </summary>
    public static bool TryLoadFromJaml(
        string jamlPath,
        out MotelyJsonConfig? config,
        out string? error
    )
    {
        config = null;
        error = null;

        if (!File.Exists(jamlPath))
        {
            error = $"File not found: {jamlPath}";
            return false;
        }

        try
        {
            var jamlContent = File.ReadAllText(jamlPath);
            return TryLoadFromJamlString(jamlContent, out config, out error);
        }
        catch (Exception ex)
        {
            config = null;
            error = $"Failed to read JAML file: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Try to load a MotelyJsonConfig from a JAML string.
    /// </summary>
    public static bool TryLoadFromJamlString(
        string jamlContent,
        out MotelyJsonConfig? config,
        out string? error
    )
    {
        config = null;
        error = null;

        try
        {
            // Parse YAML with custom node deserializer for type-as-key syntax
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithNodeDeserializer(new JamlTypeAsKeyNodeDeserializer(), s => s.Before<YamlDotNet.Serialization.NodeDeserializers.ObjectNodeDeserializer>())
                .Build();

            var deserializedConfig = deserializer.Deserialize<MotelyJsonConfig>(jamlContent);

            if (deserializedConfig == null)
            {
                error = "Failed to deserialize JAML - result was null";
                return false;
            }

            deserializedConfig.PostProcess();

            // Validate config
            MotelyJsonConfigValidator.ValidateConfig(deserializedConfig);

            config = deserializedConfig;
            return true;
        }
        catch (Exception ex)
        {
            config = null;
            // Include inner exception and stack trace for better debugging
            var innerMsg = ex.InnerException?.Message;
            var details = innerMsg != null ? $" -> {innerMsg}" : "";
            error = $"Failed to parse JAML: {ex.Message}{details}\n{ex.StackTrace}";

            return false;
        }
    }
}
