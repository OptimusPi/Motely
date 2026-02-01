using System.Text.Json.Serialization;

namespace Motely;

/// <summary>
/// AOT-compatible JSON serialization context for CLI, WASM, and API.
/// Single source for all AOT publishes.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(SeedAnalysisDto))]
[JsonSerializable(typeof(AnteAnalysisDto))]
[JsonSerializable(typeof(ShopItemDto))]
[JsonSerializable(typeof(PackDto))]
[JsonSerializable(typeof(ErrorDto))]
[JsonSerializable(typeof(SearchHitDto))]
[JsonSerializable(typeof(SearchHitDto[]))]
[JsonSerializable(typeof(SearchResponseDto))]
[JsonSerializable(typeof(ProgressDto))]
[JsonSerializable(typeof(VersionDto))]
[JsonSerializable(typeof(ValidateResultDto))]
[JsonSerializable(typeof(AnteAnalysisDto[]))]
[JsonSerializable(typeof(ShopItemDto[]))]
[JsonSerializable(typeof(PackDto[]))]
[JsonSerializable(typeof(string[]))]
public partial class MotelyAotJsonContext : JsonSerializerContext
{
}
