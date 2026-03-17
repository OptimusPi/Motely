using System.Text.Json.Serialization;
using Motely.Analysis;

namespace Motely;

/// <summary>
/// AOT-safe JSON serialisation context for the browser WASM export layer.
/// node-api-dotnet (NodeAddon) does not use JSON — it marshals real C# types
/// directly to JS objects via its compile-time source generator.
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
public partial class MotelyJsonContext : JsonSerializerContext { }
