using System.Text.Json.Serialization;
using Motely;

namespace Motely.BrowserWasm;

/// <summary>
/// AOT-compatible JSON context for WASM-only DTOs.
/// Shared DTOs (SeedAnalysisDto, ErrorDto, etc.) use MotelyAotJsonContext from the core library.
/// </summary>
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
public partial class WasmJsonContext : JsonSerializerContext { }
