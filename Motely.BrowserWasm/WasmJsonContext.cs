using System.Text.Json.Serialization;
using Motely;
using Motely.Analysis;

namespace Motely.BrowserWasm;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(CapabilitiesDto))]
[JsonSerializable(typeof(VersionDto))]
[JsonSerializable(typeof(ErrorDto))]
[JsonSerializable(typeof(ValidateResultDto))]
[JsonSerializable(typeof(SearchOptionsDto))]
[JsonSerializable(typeof(SearchStatusDto))]
[JsonSerializable(typeof(SearchHitDto[]))]
[JsonSerializable(typeof(SeedAnalysisDto))]
[JsonSerializable(typeof(AnteAnalysisDto))]
[JsonSerializable(typeof(ShopItemDto))]
[JsonSerializable(typeof(PackDto))]
public partial class WasmJsonContext : JsonSerializerContext { }
