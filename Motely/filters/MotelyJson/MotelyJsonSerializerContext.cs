using System.Text.Json.Serialization;

namespace Motely.Filters;

/// <summary>
/// AOT-compatible JSON serialization context for Motely filter types.
/// This enables Native AOT compilation by pre-generating serialization code at compile time.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
)]
[JsonSerializable(typeof(MotelyJsonConfig))]
[JsonSerializable(
    typeof(MotelyJsonConfig.MotelyJsonFilterClause),
    TypeInfoPropertyName = "MotelyJsonConfigFilterClause"
)]
[JsonSerializable(
    typeof(List<MotelyJsonConfig.MotelyJsonFilterClause>),
    TypeInfoPropertyName = "MotelyJsonConfigFilterClauseList"
)]
[JsonSerializable(typeof(MotelyFilterDefaults))]
[JsonSerializable(typeof(SourcesConfig))]
[JsonSerializable(typeof(MotelyRunConfig))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(List<string>))]
public partial class MotelyJsonSerializerContext : JsonSerializerContext { }
