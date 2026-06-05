using System.Text.Json.Serialization;

namespace Motely.Analysis;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(MotelyLegacyTextAnalysis))]
[JsonSerializable(typeof(MotelyAnteAnalysis))]
[JsonSerializable(typeof(MotelyAnalyzedItem))]
[JsonSerializable(typeof(MotelyBoosterPackAnalysis))]
[JsonSerializable(typeof(MotelyJamlyzerResult))]
[JsonSerializable(typeof(MotelyJamlyzerSeedResult))]
public partial class AnalysisJsonContext : JsonSerializerContext { }
