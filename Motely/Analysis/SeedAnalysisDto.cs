using System.Text.Json.Serialization;

namespace Motely.Analysis;

public class SeedAnalysisDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("deck")]
    public string Deck { get; set; } = "";

    [JsonPropertyName("stake")]
    public string Stake { get; set; } = "";

    [JsonPropertyName("erraticDeckComposition")]
    public string[] ErraticDeckComposition { get; set; } = [];


    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("antes")]
    public AnteAnalysisDto[] Antes { get; set; } = [];
}

public class AnteAnalysisDto
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
    public string DrawOrder { get; set; } = "";

    [JsonPropertyName("shopQueue")]
    public ShopItemDto[] ShopQueue { get; set; } = [];

    [JsonPropertyName("packs")]
    public PackDto[] Packs { get; set; } = [];
}

public class ShopItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Packed <see cref="Motely.MotelyItem"/> bits — stable key for sprite / asset lookup (e.g. Balatro image loader).</summary>
    [JsonPropertyName("value")]
    public int Value { get; set; }
}

public class PackDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("items")]
    public string[] Items { get; set; } = [];
}

[JsonSerializable(typeof(SeedAnalysisDto))]
public partial class AnalysisJsonContext : JsonSerializerContext { }
