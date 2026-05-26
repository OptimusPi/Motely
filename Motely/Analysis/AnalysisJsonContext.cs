using System.Text.Json.Serialization;

namespace Motely.Analysis;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(MotelySeedAnalysis))]
[JsonSerializable(typeof(MotelyAnteAnalysis))]
[JsonSerializable(typeof(MotelyAnalyzedItem))]
[JsonSerializable(typeof(MotelyBoosterPackAnalysis))]
public partial class AnalysisJsonContext : JsonSerializerContext { }
