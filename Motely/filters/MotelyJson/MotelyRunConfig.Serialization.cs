using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motely.Filters;

/// <summary>
/// Serialization helpers for <see cref="MotelyRunConfig"/>. This is temporary and will be
/// removed once all legacy JSON adapters are gone.
/// </summary>
public static class MotelyRunConfigSerializationExtensions
{
    public static string ToJson(this MotelyRunConfig config)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(config, options);
    }
}
