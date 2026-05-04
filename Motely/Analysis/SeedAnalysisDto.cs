using System.Text.Json.Serialization;

namespace Motely.Analysis;

public static class SeedAnalysisDtoMapper
{
    public static SeedAnalysisDto FromSeedAnalysis(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        MotelySeedAnalysis analysis
    )
    {
        var erratic = analysis.ErraticDeckComposition?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck.ToString(),
            Stake = stake.ToString(),
            ErraticDeckComposition = erratic,
            Error = analysis.Error,
            Antes = analysis
                .Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = FormatUtils.FormatBoss(a.Boss),
                    Voucher = FormatUtils.FormatVoucher(a.Voucher),
                    SmallBlindTag = FormatUtils.FormatTag(a.SmallBlindTag),
                    BigBlindTag = FormatUtils.FormatTag(a.BigBlindTag),
                    DrawOrder = a.DrawOrder ?? string.Empty,
                    ShopQueue = a
                        .ShopQueue.Select(item => new ShopItemDto
                        {
                            Id = $"ante-{a.Ante}-shop-{item.Value}",
                            Name = item.Name,
                            Value = item.Value,
                            Matched = item.Matched,
                        })
                        .ToArray(),
                    Packs = a
                        .Packs.Select((p, packIndex) => new PackDto
                        {
                            Type = FormatUtils.FormatPackName(p.Type),
                            Items = p.Items.Select((item, itemIndex) => new ShopItemDto
                            {
                                Id = $"ante-{a.Ante}-pack-{packIndex}-{itemIndex}-{item.Value}",
                                Name = item.Name,
                                Value = item.Value,
                                Matched = item.Matched,
                            }).ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}

public record class SeedAnalysisDto
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

public record class AnteAnalysisDto
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

public record class ShopItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Packed <see cref="Motely.MotelyItem"/> bits — stable key for sprite / asset lookup (e.g. Balatro image loader).</summary>
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("matched")]
    public bool Matched { get; set; }
}

public record class PackDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("items")]
    public ShopItemDto[] Items { get; set; } = [];
}

[JsonSerializable(typeof(SeedAnalysisDto))]
[JsonSerializable(typeof(AnteAnalysisDto))]
[JsonSerializable(typeof(PackDto))]
[JsonSerializable(typeof(MotelyJamlyzerResult))]
[JsonSerializable(typeof(ShopItemDto))]
public partial class AnalysisJsonContext : JsonSerializerContext { }
