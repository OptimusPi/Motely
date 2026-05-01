namespace Motely.WasmTools;

public sealed class JamlDocument
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? DateCreated { get; set; }
    public string? Description { get; set; }
    public string? Deck { get; set; }
    public string? Stake { get; set; }
    public JamlDefaults? Defaults { get; set; }
    public List<JamlCriterion>? Must { get; set; }
    public List<JamlCriterion>? Should { get; set; }
    public List<JamlCriterion>? MustNot { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<string>? Seeds { get; set; }
}

public sealed class JamlDefaults
{
    public int[]? Antes { get; set; }
    public int[]? BoosterPacks { get; set; }
    public int[]? ShopItems { get; set; }
    public int? Score { get; set; }
}

public sealed class JamlCriterion
{
    public string? Joker { get; set; }
    public List<string>? Jokers { get; set; }
    public string? CommonJoker { get; set; }
    public List<string>? CommonJokers { get; set; }
    public string? UncommonJoker { get; set; }
    public List<string>? UncommonJokers { get; set; }
    public string? RareJoker { get; set; }
    public List<string>? RareJokers { get; set; }
    public string? LegendaryJoker { get; set; }
    public string? Voucher { get; set; }
    public List<string>? Vouchers { get; set; }
    public string? Tarot { get; set; }
    public string? TarotCard { get; set; }
    public string? Spectral { get; set; }
    public string? SpectralCard { get; set; }
    public string? Planet { get; set; }
    public string? PlanetCard { get; set; }
    public string? Boss { get; set; }
    public string? Tag { get; set; }
    public string? SmallBlindTag { get; set; }
    public string? BigBlindTag { get; set; }
    public object? StandardCard { get; set; }
    public string? ErraticRank { get; set; }
    public string? ErraticSuit { get; set; }
    public string? ErraticCard { get; set; }
    public string? StartingDraw { get; set; }
    public string? Event { get; set; }
    public int[]? LuckyMoney { get; set; }
    public int[]? LuckyMult { get; set; }
    public int[]? MisprintMult { get; set; }
    public int[]? WheelOfFortune { get; set; }
    public int[]? CavendishExtinct { get; set; }
    public int[]? GrosMichelExtinct { get; set; }
    public int[]? SpaceLevelup { get; set; }
    public int[]? BusinessPayout { get; set; }
    public int[]? BloodstoneTrigger { get; set; }
    public int[]? ParkingPayout { get; set; }
    public int[]? GlassDestroy { get; set; }
    public int[]? WheelStaysFlipped { get; set; }
    public int[]? Antes { get; set; }
    public int? Score { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public string? Label { get; set; }
    public string? Edition { get; set; }
    public List<string>? Stickers { get; set; }
    public string? Seal { get; set; }
    public string? Enhancement { get; set; }
    public string? Rank { get; set; }
    public string? Suit { get; set; }
    public int[]? Rolls { get; set; }
    public int? SoulEditionRolls { get; set; }
    public bool? SoulCardOnly { get; set; }
    public List<JamlCriterion>? And { get; set; }
    public List<JamlCriterion>? Or { get; set; }
    public List<JamlCriterion>? Clauses { get; set; }
    public string? Mode { get; set; }
    public int[]? ShopItems { get; set; }
    public int[]? BoosterPacks { get; set; }
    public int? MinShopItem { get; set; }
    public int? MaxShopItem { get; set; }
    public JamlSources? Sources { get; set; }
}

public sealed class JamlSources
{
    public int[]? ShopItems { get; set; }
    public int[]? BoosterPacks { get; set; }
    public int? MinShopItem { get; set; }
    public int? MaxShopItem { get; set; }
    public bool? RequireMega { get; set; }
}
