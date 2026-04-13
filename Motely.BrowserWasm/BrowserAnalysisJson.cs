#nullable enable
using System.Text.Json.Serialization;

namespace Motely.BrowserWasm;

internal sealed class BrowserSeedAnalysisDto
{
    [JsonPropertyName("seed")]
    public string? Seed { get; set; }

    [JsonPropertyName("deck")]
    public string? Deck { get; set; }

    [JsonPropertyName("erraticDeckComposition")]
    public string? ErraticDeckComposition { get; set; }

    [JsonPropertyName("antes")]
    public BrowserAnteAnalysisDto[] Antes { get; set; } = [];
}

internal sealed class BrowserSeedAnalysisErrorDto
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

internal sealed class BrowserAnteAnalysisDto
{
    [JsonPropertyName("ante")]
    public int Ante { get; set; }

    [JsonPropertyName("boss")]
    public string Boss { get; set; } = "";

    [JsonPropertyName("voucher")]
    public string Voucher { get; set; } = "";

    [JsonPropertyName("smallBlindTag")]
    public string SmallBlindTag { get; set; } = "";

    [JsonPropertyName("bigBlindTag")]
    public string BigBlindTag { get; set; } = "";

    [JsonPropertyName("drawOrder")]
    public string? DrawOrder { get; set; }

    [JsonPropertyName("shopQueue")]
    public string[] ShopQueue { get; set; } = [];

    [JsonPropertyName("packs")]
    public BrowserPackAnalysisDto[] Packs { get; set; } = [];
}

internal sealed class BrowserPackAnalysisDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("items")]
    public string[] Items { get; set; } = [];
}

[JsonSerializable(typeof(BrowserSeedAnalysisDto))]
[JsonSerializable(typeof(BrowserSeedAnalysisErrorDto))]
internal partial class BrowserAnalysisJsonContext : JsonSerializerContext;
