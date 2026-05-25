using System.Diagnostics.CodeAnalysis;
using SharpYaml;
using SharpYaml.Serialization;

namespace Motely.Filters.Jaml;

public static class JamlConfigLoader
{
    public static bool TryLoad(string yaml, [NotNullWhen(true)] out JamlConfig? config, [NotNullWhen(false)] out string? error)
    {
        try
        {
            config = YamlSerializer.Deserialize<JamlConfig>(yaml, JamlSerializerContext.Default.JamlConfig);
            if (config is null)
            {
                error = "YAML deserialized to null (empty document?).";
                return false;
            }
            error = null;
            return true;
        }
        catch (System.Exception ex)
        {
            config = null;
            error = ex.Message;
            return false;
        }
    }
}
