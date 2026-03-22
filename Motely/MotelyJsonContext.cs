using System.Text.Json.Serialization;
using Motely.Analysis;

namespace Motely;

/// <summary>
/// AOT-safe JSON serialisation context for the browser WASM export layer.
/// Node.js native interop (if used) typically marshals C# types directly to JS
/// without this JSON context; this type is for WASM / JSON-shaped DTOs.
/// </summary>
[JsonSerializable(typeof(VersionDto))]
[JsonSerializable(typeof(CapabilitiesDto))]
[JsonSerializable(typeof(ValidateResultDto))]
[JsonSerializable(typeof(SearchOptionsDto))]
[JsonSerializable(typeof(SearchStatusDto))]
[JsonSerializable(typeof(SearchHitDto))]
[JsonSerializable(typeof(SearchHitDto[]))]
[JsonSerializable(typeof(ErrorDto))]
[JsonSerializable(typeof(SeedAnalysisDto))]
[JsonSerializable(typeof(AnteAnalysisDto))]
[JsonSerializable(typeof(AnteAnalysisDto[]))]
[JsonSerializable(typeof(ShopItemDto))]
[JsonSerializable(typeof(ShopItemDto[]))]
[JsonSerializable(typeof(PackDto))]
[JsonSerializable(typeof(PackDto[]))]
[JsonSerializable(typeof(LuckyMoneyStreamDto))]
[JsonSerializable(typeof(IntStreamDto))]
[JsonSerializable(typeof(StringStreamDto))]
[JsonSerializable(typeof(PackStreamDto))]
[JsonSerializable(typeof(ItemStreamDto))]
[JsonSerializable(typeof(double[]))]
[JsonSerializable(typeof(int[]))]
public partial class MotelyJsonContext : JsonSerializerContext { }
