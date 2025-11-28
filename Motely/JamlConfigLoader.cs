using Motely.Filters;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

            // Parse JAML (YAML-based) to object
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
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
            error = $"Failed to parse JAML: {ex.Message}";
            return false;
        }
    }
}
