using Motely.Filters;
using YamlDotNet.Serialization;

namespace MotelyYaml;

/// <summary>
/// AOT-compatible YAML serialization context for Motely filter types.
/// The YamlDotNet.Analyzers.StaticGenerator will generate static serialization code for these types.
/// Uses a separate namespace to avoid conflicts with Motely project name.
/// Note: Only register concrete classes, not generic collections (generator doesn't support them).
/// </summary>
[YamlStaticContext]
[YamlSerializable(typeof(MotelyJsonConfig))]
[YamlSerializable(typeof(MotelyFilterDefaults))]
[YamlSerializable(typeof(SourcesConfig))]
// Note: MotelyJsonFilterClause excluded - has 'required' properties incompatible with source generator
public partial class MotelyYamlStaticContext : StaticContext
{
}
