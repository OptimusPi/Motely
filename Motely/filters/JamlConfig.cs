
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace Motely.Filters;

/// <summary>
/// Typed clause lists for one JAML section (must / should / mustNot).
/// Each element = one filter in the chain.
/// </summary>
public sealed class JamlClauseSet
{
    public List<JokerClause> Jokers { get; set; } = [];
    public List<CommonJokerClause> CommonJokers { get; set; } = [];
    public List<UncommonJokerClause> UncommonJokers { get; set; } = [];
    public List<RareJokerClause> RareJokers { get; set; } = [];
    public List<MixedJokerClause> MixedJokers { get; set; } = [];
    public List<LegendaryJokerClause> LegendaryJokers { get; set; } = [];
    public List<VoucherClause> Vouchers { get; set; } = [];
    public List<TarotCardClause> TarotCards { get; set; } = [];
    public List<SpectralCardClause> SpectralCards { get; set; } = [];
    public List<PlanetCardClause> PlanetCards { get; set; } = [];
    public List<StandardCardClause> StandardCards { get; set; } = [];
    public List<BossClause> Bosses { get; set; } = [];
    public List<TagClause> Tags { get; set; } = [];
    public List<ErraticRankClause> ErraticRanks { get; set; } = [];
    public List<ErraticSuitClause> ErraticSuits { get; set; } = [];
    public List<ErraticCardClause> ErraticCards { get; set; } = [];
    public List<LuckyMoneyClause> LuckyMoney { get; set; } = [];
    public List<LuckyMultClause> LuckyMult { get; set; } = [];
    public List<MisprintMultClause> MisprintMult { get; set; } = [];
    public List<WheelOfFortuneClause> WheelOfFortune { get; set; } = [];
    public List<CavendishExtinctClause> CavendishExtinct { get; set; } = [];
    public List<GrosMichelExtinctClause> GrosMichelExtinct { get; set; } = [];
    public List<StartingDrawClause> StartingDraw { get; set; } = [];

    public bool HasAnyClauses =>
        Jokers.Count > 0 || CommonJokers.Count > 0 || UncommonJokers.Count > 0 ||
        RareJokers.Count > 0 || MixedJokers.Count > 0 || LegendaryJokers.Count > 0 ||
        Vouchers.Count > 0 || TarotCards.Count > 0 || SpectralCards.Count > 0 ||
        PlanetCards.Count > 0 || StandardCards.Count > 0 ||
        Bosses.Count > 0 || Tags.Count > 0 ||
        ErraticRanks.Count > 0 || ErraticSuits.Count > 0 || ErraticCards.Count > 0 ||
        LuckyMoney.Count > 0 || LuckyMult.Count > 0 || MisprintMult.Count > 0 ||
        WheelOfFortune.Count > 0 || CavendishExtinct.Count > 0 || GrosMichelExtinct.Count > 0 ||
        StartingDraw.Count > 0;
}

/// <summary>
/// JAML config consumed by JamlSearchBuilder.
/// </summary>
public sealed class JamlConfig
{
    public string? Name { get; set; }
    public MotelyDeck Deck { get; set; } = MotelyDeck.Red;
    public MotelyStake Stake { get; set; } = MotelyStake.White;

    public JamlClauseSet Must { get; set; } = new();
    public JamlClauseSet Should { get; set; } = new();
    public JamlClauseSet MustNot { get; set; } = new();

    public bool HasAnyClauses => Must.HasAnyClauses || Should.HasAnyClauses || MustNot.HasAnyClauses;
}
// Flat data bags for YamlDotNet deserialization. Match jaml.schema.json.

public sealed class JamlDto
{
    [YamlMember(Alias = "name")] public string? Name { get; set; }
    [YamlMember(Alias = "author")] public string? Author { get; set; }
    [YamlMember(Alias = "description")] public string? Description { get; set; }
    [YamlMember(Alias = "deck")] public string? Deck { get; set; }
    [YamlMember(Alias = "stake")] public string? Stake { get; set; }
    [YamlMember(Alias = "defaults")] public JamlDefaultsDto? Defaults { get; set; }
    [YamlMember(Alias = "must")] public List<JamlClauseDto>? Must { get; set; }
    [YamlMember(Alias = "should")] public List<JamlClauseDto>? Should { get; set; }
    [YamlMember(Alias = "mustNot")] public List<JamlClauseDto>? MustNot { get; set; }
    [YamlMember(Alias = "seeds")] public List<string>? Seeds { get; set; }
}

public sealed class JamlDefaultsDto
{
    [YamlMember(Alias = "antes")] public int[]? Antes { get; set; }
    [YamlMember(Alias = "boosterPacks")] public int[]? BoosterPacks { get; set; }
    [YamlMember(Alias = "shopItems")] public int[]? ShopItems { get; set; }
    [YamlMember(Alias = "score")] public int? Score { get; set; }
}

public sealed class JamlClauseDto
{
    // Explicit type+value (old syntax)
    [YamlMember(Alias = "type")] public string? Type { get; set; }
    [YamlMember(Alias = "value")] public string? Value { get; set; }

    // Type-as-key shorthand (new syntax)
    [YamlMember(Alias = "joker")] public string? Joker { get; set; }
    [YamlMember(Alias = "jokers")] public List<string>? Jokers { get; set; }
    [YamlMember(Alias = "commonJoker")] public string? CommonJoker { get; set; }
    [YamlMember(Alias = "commonJokers")] public List<string>? CommonJokers { get; set; }
    [YamlMember(Alias = "uncommonJoker")] public string? UncommonJoker { get; set; }
    [YamlMember(Alias = "uncommonJokers")] public List<string>? UncommonJokers { get; set; }
    [YamlMember(Alias = "rareJoker")] public string? RareJoker { get; set; }
    [YamlMember(Alias = "rareJokers")] public List<string>? RareJokers { get; set; }
    [YamlMember(Alias = "mixedJoker")] public string? MixedJoker { get; set; }
    [YamlMember(Alias = "mixedJokers")] public List<string>? MixedJokers { get; set; }
    [YamlMember(Alias = "legendaryJoker")] public string? SoulJoker { get; set; }
    [YamlMember(Alias = "voucher")] public string? Voucher { get; set; }
    [YamlMember(Alias = "vouchers")] public List<string>? Vouchers { get; set; }
    [YamlMember(Alias = "tarot")] public string? Tarot { get; set; }
    [YamlMember(Alias = "tarotCard")] public string? TarotCard { get; set; }
    [YamlMember(Alias = "spectral")] public string? Spectral { get; set; }
    [YamlMember(Alias = "spectralCard")] public string? SpectralCard { get; set; }
    [YamlMember(Alias = "planet")] public string? Planet { get; set; }
    [YamlMember(Alias = "planetCard")] public string? PlanetCard { get; set; }
    [YamlMember(Alias = "boss")] public string? Boss { get; set; }
    [YamlMember(Alias = "tag")] public string? Tag { get; set; }
    [YamlMember(Alias = "smallBlindTag")] public string? SmallBlindTag { get; set; }
    [YamlMember(Alias = "bigBlindTag")] public string? BigBlindTag { get; set; }
    [YamlMember(Alias = "standardCard")] public string? StandardCard { get; set; }
    [YamlMember(Alias = "erraticRank")] public string? ErraticRank { get; set; }
    [YamlMember(Alias = "erraticSuit")] public string? ErraticSuit { get; set; }
    [YamlMember(Alias = "erraticCard")] public string? ErraticCard { get; set; }
    [YamlMember(Alias = "startingDraw")] public string? StartingDraw { get; set; }
    [YamlMember(Alias = "event")] public string? Event { get; set; }
    [YamlMember(Alias = "eventType")] public string? EventType { get; set; }

    // Common clause properties
    [YamlMember(Alias = "antes")] public int[]? Antes { get; set; }
    [YamlMember(Alias = "score")] public int? Score { get; set; }
    [YamlMember(Alias = "min")] public int? Min { get; set; }
    [YamlMember(Alias = "max")] public int? Max { get; set; }
    [YamlMember(Alias = "label")] public string? Label { get; set; }
    [YamlMember(Alias = "edition")] public string? Edition { get; set; }
    [YamlMember(Alias = "stickers")] public string[]? Stickers { get; set; }
    [YamlMember(Alias = "seal")] public string? Seal { get; set; }
    [YamlMember(Alias = "enhancement")] public string? Enhancement { get; set; }
    [YamlMember(Alias = "rank")] public string? Rank { get; set; }
    [YamlMember(Alias = "suit")] public string? Suit { get; set; }
    [YamlMember(Alias = "rolls")] public int[]? Rolls { get; set; }

    // Compound clauses
    [YamlMember(Alias = "and")] public List<JamlClauseDto>? And { get; set; }
    [YamlMember(Alias = "or")] public List<JamlClauseDto>? Or { get; set; }

    // Flat source shortcuts (top-level on clause)
    [YamlMember(Alias = "shopItems")] public int[]? ShopItems { get; set; }
    [YamlMember(Alias = "boosterPacks")] public int[]? BoosterPacks { get; set; }
    
    [YamlMember(Alias = "minShopSlot")] public int? MinShopSlot { get; set; }
    [YamlMember(Alias = "maxShopSlot")] public int? MaxShopSlot { get; set; }
    [YamlMember(Alias = "minPackSlot")] public int? MinPackSlot { get; set; }
    [YamlMember(Alias = "maxPackSlot")] public int? MaxPackSlot { get; set; }

    // Nested sources object
    [YamlMember(Alias = "sources")] public JamlSourcesDto? Sources { get; set; }
}

public sealed class JamlSourcesDto
{
    [YamlMember(Alias = "shopItems")] public int[]? ShopItems { get; set; }
    [YamlMember(Alias = "boosterPacks")] public int[]? BoosterPacks { get; set; }
    [YamlMember(Alias = "minShopSlot")] public int? MinShopSlot { get; set; }
    [YamlMember(Alias = "maxShopSlot")] public int? MaxShopSlot { get; set; }
    [YamlMember(Alias = "minPackSlot")] public int? MinPackSlot { get; set; }
    [YamlMember(Alias = "maxPackSlot")] public int? MaxPackSlot { get; set; }
    [YamlMember(Alias = "tags")] public bool Tags { get; set; }
    [YamlMember(Alias = "requireMega")] public bool RequireMega { get; set; }
    [YamlMember(Alias = "judgement")] public int[]? Judgement { get; set; }
    [YamlMember(Alias = "rareTag")] public int[]? RareTag { get; set; }
    [YamlMember(Alias = "uncommonTag")] public int[]? UncommonTag { get; set; }
    [YamlMember(Alias = "wraith")] public int[]? Wraith { get; set; }
    [YamlMember(Alias = "soulCard")] public int[]? SoulCard { get; set; }
    [YamlMember(Alias = "riffRaff")] public int[]? RiffRaff { get; set; }
    [YamlMember(Alias = "purpleSealOrEightBall")] public int[]? PurpleSealOrEightBall { get; set; }
    [YamlMember(Alias = "emperor")] public int[]? Emperor { get; set; }
    [YamlMember(Alias = "sixthSense")] public int[]? SixthSense { get; set; }
    [YamlMember(Alias = "seance")] public int[]? Seance { get; set; }
    [YamlMember(Alias = "certificate")] public int[]? Certificate { get; set; }
    [YamlMember(Alias = "incantation")] public int[]? Incantation { get; set; }
    [YamlMember(Alias = "familiar")] public int[]? Familiar { get; set; }
    [YamlMember(Alias = "grim")] public int[]? Grim { get; set; }
    [YamlMember(Alias = "deckDraw")] public int[]? DeckDraw { get; set; }
    [YamlMember(Alias = "uncommonShopJokers")] public int[]? UncommonShopJokers { get; set; }

    [YamlMember(Alias = "rareShopJokers")] public int[]? RareShopJokers { get; set; }
    [YamlMember(Alias = "commonShopJokers")] public int[]? CommonShopJokers { get; set; }
    [YamlMember(Alias = "allShopJokers")] public int[]? AllShopJokers { get; set; }
}

// ────────────────────────────── Loader ──────────────────────────────

public static class JamlConfigLoader
{
    private static readonly int[] DefaultAntes = [1, 2, 3, 4, 5, 6, 7, 8];

    public static bool TryLoad(
        string jaml,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error)
    {
        config = null;
        error = null;

        if (string.IsNullOrWhiteSpace(jaml))
        {
            error = "JAML content is required.";
            return false;
        }

        try
        {
            var deserializer = new StaticDeserializerBuilder(new JamlYamlContext())
                .IgnoreUnmatchedProperties()
                .Build();
            var dto = deserializer.Deserialize<JamlDto>(jaml);
            if (dto == null)
            {
                error = "Failed to deserialize JAML content.";
                return false;
            }

            var defaultAntes = dto.Defaults?.Antes ?? DefaultAntes;

            var deck = Enum.TryParse<MotelyDeck>(dto.Deck, true, out var deckEnum)
                ? deckEnum : MotelyDeck.Red;
            var stake = Enum.TryParse<MotelyStake>(dto.Stake, true, out var stakeEnum)
                ? stakeEnum : MotelyStake.White;

            config = new JamlConfig
            {
                Name = dto.Name,
                Deck = deck,
                Stake = stake,
            };

            // MUST → required filters
            PopulateClauses(config.Must, dto.Must, defaultAntes, dto.Defaults);

            // SHOULD → scoring clauses
            PopulateClauses(config.Should, dto.Should, defaultAntes, dto.Defaults);

            // MUSTNOT → negation filters
            PopulateClauses(config.MustNot, dto.MustNot, defaultAntes, dto.Defaults);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryLoadFromFile(
        string path,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error)
    {
        config = null;
        var resolved = ResolveJamlPath(path);
        if (resolved == null)
        {
            error = $"File not found: {path}";
            return false;
        }
        return TryLoad(File.ReadAllText(resolved), out config, out error);
    }

    private static string? ResolveJamlPath(string path)
    {
        // Exact path
        if (File.Exists(path)) return path;
        // Add .jaml extension
        var withExt = Path.ChangeExtension(path, ".jaml");
        if (File.Exists(withExt)) return withExt;
        // Check JamlFilters/ subdirectory
        var inFilters = Path.Combine("JamlFilters", path);
        if (File.Exists(inFilters)) return inFilters;
        var inFiltersExt = Path.Combine("JamlFilters", withExt);
        if (File.Exists(inFiltersExt)) return inFiltersExt;
        return null;
    }

    // ── Clause list population — adds directly to typed lists or logic clauses ──

    private static void PopulateClauses(JamlClauseSet set, List<JamlClauseDto>? clauses, int[] defaultAntes, JamlDefaultsDto? defaults)
    {
        if (clauses == null || clauses.Count == 0) return;
        foreach (var c in clauses)
        {
            var clause = CreateClauseFromDto(c, defaultAntes, defaults);
            AddClauseToSet(set, clause);
        }
    }

    private static void AddClauseToSet(JamlClauseSet set, IJamlClause clause)
    {
        switch (clause)
        {
            case JokerClause c: set.Jokers.Add(c); break;
            case CommonJokerClause c: set.CommonJokers.Add(c); break;
            case UncommonJokerClause c: set.UncommonJokers.Add(c); break;
            case RareJokerClause c: set.RareJokers.Add(c); break;
            case MixedJokerClause c: set.MixedJokers.Add(c); break;
            case LegendaryJokerClause c: set.LegendaryJokers.Add(c); break;
            case VoucherClause c: set.Vouchers.Add(c); break;
            case TarotCardClause c: set.TarotCards.Add(c); break;
            case SpectralCardClause c: set.SpectralCards.Add(c); break;
            case PlanetCardClause c: set.PlanetCards.Add(c); break;
            case BossClause c: set.Bosses.Add(c); break;
            case TagClause c: set.Tags.Add(c); break;
            case StandardCardClause c: set.StandardCards.Add(c); break;
            case ErraticRankClause c: set.ErraticRanks.Add(c); break;
            case ErraticSuitClause c: set.ErraticSuits.Add(c); break;
            case ErraticCardClause c: set.ErraticCards.Add(c); break;
            case LuckyMoneyClause c: set.LuckyMoney.Add(c); break;
            case LuckyMultClause c: set.LuckyMult.Add(c); break;
            case MisprintMultClause c: set.MisprintMult.Add(c); break;
            case WheelOfFortuneClause c: set.WheelOfFortune.Add(c); break;
            case CavendishExtinctClause c: set.CavendishExtinct.Add(c); break;
            case GrosMichelExtinctClause c: set.GrosMichelExtinct.Add(c); break;
            case StartingDrawClause c: set.StartingDraw.Add(c); break;
            case AndClause: break; // logic combinators not yet dispatched to filter descs
            case OrClause: break;
            default:
                throw new NotSupportedException($"Unsupported clause type: {clause.GetType().Name}");
        }
    }

    private static IJamlClause CreateClauseFromDto(JamlClauseDto c, int[] defaultAntes, JamlDefaultsDto? defaults)
    {
        var antes = c.Antes ?? defaultAntes;
        int min = c.Min ?? 1;
        int score = c.Score ?? 1;

        if (c.And != null)
        {
            return new AndClause
            {
                Score = score,
                Clauses = c.And.Select(sub => CreateClauseFromDto(sub, [], defaults)).ToArray()
            };
        }
        if (c.Or != null)
        {
            return new OrClause
            {
                Score = score,
                Min = min,
                Clauses = c.Or.Select(sub => CreateClauseFromDto(sub, [], defaults)).ToArray()
            };
        }

        var (itemType, value) = ResolveType(c);
        var edition = ParseEnum<MotelyItemEdition>(c.Edition);
        var label = c.Label ?? JamlClauseLabeler.Generate(itemType, c, antes, min);

        var shopItems = c.Sources?.ShopItems ?? c.ShopItems ?? defaults?.ShopItems;
        var boosterPacks = c.Sources?.BoosterPacks ?? c.BoosterPacks ?? defaults?.BoosterPacks;

        // Support top-level range generators (e.g. minShopSlot: 0)
        var minShop = c.Sources?.MinShopSlot ?? c.MinShopSlot;
        var maxShop = c.Sources?.MaxShopSlot ?? c.MaxShopSlot;
        var minPack = c.Sources?.MinPackSlot ?? c.MinPackSlot;
        var maxPack = c.Sources?.MaxPackSlot ?? c.MaxPackSlot;

        if (shopItems == null && minShop != null && maxShop != null)
            shopItems = Enumerable.Range(minShop.Value, maxShop.Value - minShop.Value + 1).ToArray();
            
        if (boosterPacks == null && minPack != null && maxPack != null)
            boosterPacks = Enumerable.Range(minPack.Value, maxPack.Value - minPack.Value + 1).ToArray();

        return itemType switch
        {
            MotelyFilterItemType.Joker => new JokerClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Jokers = value != null
                    ? [Enum.Parse<MotelyJoker>(value, true)]
                    : c.Jokers?.Select(j => Enum.Parse<MotelyJoker>(j, true)).ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers?.Select(s => Enum.Parse<MotelyJokerSticker>(s, true)).ToArray() ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                },
            },
            MotelyFilterItemType.CommonJoker => new CommonJokerClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Jokers = value != null
                    ? [Enum.Parse<MotelyJokerCommon>(value, true)]
                    : (c.CommonJokers ?? c.Jokers)?.Select(j => Enum.Parse<MotelyJokerCommon>(j, true)).ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers?.Select(s => Enum.Parse<MotelyJokerSticker>(s, true)).ToArray() ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                },
            },
            MotelyFilterItemType.UncommonJoker => new UncommonJokerClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Jokers = value != null
                    ? [Enum.Parse<MotelyJokerUncommon>(value, true)]
                    : (c.UncommonJokers ?? c.Jokers)?.Select(j => Enum.Parse<MotelyJokerUncommon>(j, true)).ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers?.Select(s => Enum.Parse<MotelyJokerSticker>(s, true)).ToArray() ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                },
            },
            MotelyFilterItemType.RareJoker => new RareJokerClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Jokers = value != null
                    ? [Enum.Parse<MotelyJokerRare>(value, true)]
                    : (c.RareJokers ?? c.Jokers)?.Select(j => Enum.Parse<MotelyJokerRare>(j, true)).ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers?.Select(s => Enum.Parse<MotelyJokerSticker>(s, true)).ToArray() ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                },
            },
            MotelyFilterItemType.MixedJoker => new MixedJokerClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Jokers = value != null
                    ? [Enum.Parse<MotelyJoker>(value, true)]
                    : (c.MixedJokers ?? c.Jokers)?.Select(j => Enum.Parse<MotelyJoker>(j, true)).ToArray() ?? [],
                Edition = edition,
                Stickers = c.Stickers?.Select(s => Enum.Parse<MotelyJokerSticker>(s, true)).ToArray() ?? [],
                Sources = new JokerSourceConfig
                {
                    ShopItems = shopItems ?? [],
                    BoosterPacks = boosterPacks ?? [],
                    Judgement = c.Sources?.Judgement ?? [],
                    Wraith = c.Sources?.Wraith ?? [],
                    RiffRaff = c.Sources?.RiffRaff ?? [],
                    RareTag = c.Sources?.RareTag ?? [],
                    UncommonTag = c.Sources?.UncommonTag ?? [],
                },
            },
            MotelyFilterItemType.SoulJoker => new LegendaryJokerClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Jokers = value != null
                    ? [Enum.Parse<MotelyJoker>(value, true)]
                    : c.Jokers?.Select(j => Enum.Parse<MotelyJoker>(j, true)).ToArray() ?? [],
                Edition = edition,
                Sources = new SoulJokerSourceConfig
                {
                    ShopItems = shopItems ?? [], BoosterPacks = boosterPacks ?? [],
                    SoulCard = c.Sources?.SoulCard ?? [],
                },
            },
            MotelyFilterItemType.Voucher => new VoucherClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Vouchers = value != null
                    ? [Enum.Parse<MotelyVoucher>(value, true)]
                    : c.Vouchers?.Select(v => Enum.Parse<MotelyVoucher>(v, true)).ToArray() ?? [],
            },
            MotelyFilterItemType.TarotCard => new TarotCardClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Tarots = value != null ? [Enum.Parse<MotelyTarotCard>(value, true)] : [],
                Sources = new TarotCardSourceConfig
                {
                    ShopItems = shopItems ?? [], BoosterPacks = boosterPacks ?? [],
                    Emperor = c.Sources?.Emperor ?? [],
                    PurpleSealOrEightBall = c.Sources?.PurpleSealOrEightBall ?? [],
                },
            },
            MotelyFilterItemType.SpectralCard => new SpectralCardClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Spectrals = value != null ? [Enum.Parse<MotelySpectralCard>(value, true)] : [],
                Sources = new SpectralCardSourceConfig
                {
                    ShopItems = shopItems ?? [], BoosterPacks = boosterPacks ?? [],
                    SixthSense = c.Sources?.SixthSense ?? [],
                    Seance = c.Sources?.Seance ?? [],
                },
            },
            MotelyFilterItemType.PlanetCard => new PlanetCardClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Planets = value != null ? [Enum.Parse<MotelyPlanetCard>(value, true)] : [],
                Sources = new PlanetSourceConfig
                {
                    ShopItems = shopItems ?? [], BoosterPacks = boosterPacks ?? [],
                },
            },
            MotelyFilterItemType.Boss => new BossClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Bosses = value != null ? [Enum.Parse<MotelyBossBlind>(value, true)] : [],
            },
            MotelyFilterItemType.SmallBlindTag => new TagClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Tags = value != null ? [Enum.Parse<MotelyTag>(value, true)] : [],
                Position = TagPosition.SmallBlind,
            },
            MotelyFilterItemType.BigBlindTag => new TagClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Tags = value != null ? [Enum.Parse<MotelyTag>(value, true)] : [],
                Position = TagPosition.BigBlind,
            },
            MotelyFilterItemType.PlayingCard => new StandardCardClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Rank = ParseEnum<MotelyPlayingCardRank>(c.Rank),
                Suit = ParseEnum<MotelyPlayingCardSuit>(c.Suit),
                Enhancement = ParseEnum<MotelyItemEnhancement>(c.Enhancement),
                Seal = ParseEnum<MotelyItemSeal>(c.Seal),
                Edition = edition,
                Sources = new StandardCardSourceConfig
                {
                    ShopItems = shopItems ?? [], BoosterPacks = boosterPacks ?? [],
                    Certificate = c.Sources?.Certificate ?? [], Incantation = c.Sources?.Incantation ?? [],
                    Familiar = c.Sources?.Familiar ?? [], Grim = c.Sources?.Grim ?? [],
                    DeckDraw = c.Sources?.DeckDraw ?? [],
                },
            },
            MotelyFilterItemType.ErraticRank => new ErraticRankClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Rank = ParseEnum<MotelyPlayingCardRank>(c.Rank ?? value)
                    ?? throw new NotSupportedException("ErraticRank clause requires a rank value."),
            },
            MotelyFilterItemType.ErraticSuit => new ErraticSuitClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Suit = ParseEnum<MotelyPlayingCardSuit>(c.Suit ?? value)
                    ?? throw new NotSupportedException("ErraticSuit clause requires a suit value."),
            },
            MotelyFilterItemType.ErraticCard => CreateErraticCardClause(c, value, antes, min, score),
            MotelyFilterItemType.StartingDraw => new StartingDrawClause
            {
                Label = label, Score = score, Antes = antes, Min = min,
                Rank = ParseEnum<MotelyPlayingCardRank>(c.Rank),
                Suit = ParseEnum<MotelyPlayingCardSuit>(c.Suit),
            },
            MotelyFilterItemType.Event => CreateEventClause(c.Event, c.Rolls, antes, min, score),
            _ => throw new NotSupportedException($"Unsupported clause type: {itemType}"),
        };
    }

    private static ErraticCardClause CreateErraticCardClause(JamlClauseDto c, string? value, int[] antes, int min, int score)
    {
        var rank = ParseEnum<MotelyPlayingCardRank>(c.Rank ?? value);
        var suit = ParseEnum<MotelyPlayingCardSuit>(c.Suit ?? value);

        if (rank != null && suit != null)
        {
            return new ErraticCardClause
            {
                Score = score, Antes = antes, Min = min,
                Rank = rank.Value,
                Suit = suit.Value,
            };
        }
        // If not both, we can't create ErraticCardClause. But ResolveType logic mapped rank-only to ErraticRank, etc.
        // If we got here with itemType == ErraticCard, we expect both OR explicit 'ErraticCard' type tag.
        // But if user provided Rank OR Suit only, ResolveType mapped to ErraticRank/Suit.
        // So this method ONLY handles the true ErraticCard case (both present).
        // Wait, ResolveType maps c.ErraticCard != null -> ErraticCard.
        // What if c.ErraticCard (key) is used but only rank provided?
        // We should throw or handle.
        // I will throw if incomplete, because if it was mapped here, user intended ErraticCard.

        if (c.Type == "ErraticCard" && (rank == null || suit == null))
            throw new NotSupportedException("ErraticCard clause requires both Rank and Suit.");

        throw new NotSupportedException("ErraticCard clause requires rank and suit.");
    }

    private static IRollClause CreateEventClause(string? eventName, int[]? rolls, int[] antes, int min, int score)
    {
        if (string.IsNullOrEmpty(eventName))
            throw new NotSupportedException("Event clause is missing event type name.");

        var r = rolls ?? [];
        return Enum.Parse<MotelyEventType>(eventName, true) switch
        {
            MotelyEventType.LuckyMoney => new LuckyMoneyClause { Score = score, Antes = antes, Min = min, Rolls = r },
            MotelyEventType.LuckyMult => new LuckyMultClause { Score = score, Antes = antes, Min = min, Rolls = r },
            MotelyEventType.MisprintMult => new MisprintMultClause { Score = score, Antes = antes, Min = min, Rolls = r },
            MotelyEventType.WheelOfFortune => new WheelOfFortuneClause { Score = score, Antes = antes, Min = min, Rolls = r },
            MotelyEventType.CavendishExtinct => new CavendishExtinctClause { Score = score, Antes = antes, Min = min, Rolls = r },
            MotelyEventType.GrosMichelExtinct => new GrosMichelExtinctClause { Score = score, Antes = antes, Min = min, Rolls = r },
            _ => throw new NotSupportedException($"Unsupported event type: {eventName}")
        };
    }

    // ── Resolve type from shorthand keys or explicit type field ──

    private static (MotelyFilterItemType itemType, string? value) ResolveType(JamlClauseDto c)
    {
        // Shorthand keys (type-as-key) — check each one
        if (c.Joker != null) return (MotelyFilterItemType.Joker, c.Joker);
        if (c.Jokers != null) return (MotelyFilterItemType.Joker, null); // plural
        if (c.CommonJoker != null) return (MotelyFilterItemType.CommonJoker, c.CommonJoker);
        if (c.CommonJokers != null) return (MotelyFilterItemType.CommonJoker, null);
        if (c.UncommonJoker != null) return (MotelyFilterItemType.UncommonJoker, c.UncommonJoker);
        if (c.UncommonJokers != null) return (MotelyFilterItemType.UncommonJoker, null);
        if (c.RareJoker != null) return (MotelyFilterItemType.RareJoker, c.RareJoker);
        if (c.RareJokers != null) return (MotelyFilterItemType.RareJoker, null);
        if (c.MixedJoker != null) return (MotelyFilterItemType.MixedJoker, c.MixedJoker);
        if (c.MixedJokers != null) return (MotelyFilterItemType.MixedJoker, null);
        if (c.SoulJoker != null) return (MotelyFilterItemType.SoulJoker, c.SoulJoker);
        if (c.Voucher != null) return (MotelyFilterItemType.Voucher, c.Voucher);
        if (c.Vouchers != null) return (MotelyFilterItemType.Voucher, null);
        if (c.Tarot != null) return (MotelyFilterItemType.TarotCard, c.Tarot);
        if (c.TarotCard != null) return (MotelyFilterItemType.TarotCard, c.TarotCard);
        if (c.Spectral != null) return (MotelyFilterItemType.SpectralCard, c.Spectral);
        if (c.SpectralCard != null) return (MotelyFilterItemType.SpectralCard, c.SpectralCard);
        if (c.Planet != null) return (MotelyFilterItemType.PlanetCard, c.Planet);
        if (c.PlanetCard != null) return (MotelyFilterItemType.PlanetCard, c.PlanetCard);
        if (c.Boss != null) return (MotelyFilterItemType.Boss, c.Boss);
        if (c.Tag != null) return (MotelyFilterItemType.SmallBlindTag, c.Tag);
        if (c.SmallBlindTag != null) return (MotelyFilterItemType.SmallBlindTag, c.SmallBlindTag);
        if (c.BigBlindTag != null) return (MotelyFilterItemType.BigBlindTag, c.BigBlindTag);
        if (c.StandardCard != null) return (MotelyFilterItemType.PlayingCard, c.StandardCard);
        if (c.ErraticRank != null) return (MotelyFilterItemType.ErraticRank, c.ErraticRank);
        if (c.ErraticSuit != null) return (MotelyFilterItemType.ErraticSuit, c.ErraticSuit);
        if (c.ErraticCard != null) return (MotelyFilterItemType.ErraticCard, c.ErraticCard);
        if (c.StartingDraw != null) return (MotelyFilterItemType.StartingDraw, c.StartingDraw);
        if (c.Event != null) return (MotelyFilterItemType.Event, c.Event);

        // Explicit type+value
        if (c.Type != null)
        {
            var itemType = ParseItemType(c.Type);
            return (itemType, c.Value ?? c.EventType);
        }

        throw new InvalidOperationException("Clause has no type key or shorthand key.");
    }

    private static MotelyFilterItemType ParseItemType(string type) => type switch
    {
        "Joker" => MotelyFilterItemType.Joker,
        "CommonJoker" => MotelyFilterItemType.CommonJoker,
        "UncommonJoker" => MotelyFilterItemType.UncommonJoker,
        "RareJoker" => MotelyFilterItemType.RareJoker,
        "MixedJoker" => MotelyFilterItemType.MixedJoker,
        "SoulJoker" => MotelyFilterItemType.SoulJoker,
        "Voucher" => MotelyFilterItemType.Voucher,
        "TarotCard" => MotelyFilterItemType.TarotCard,
        "Planet" or "PlanetCard" => MotelyFilterItemType.PlanetCard,
        "Spectral" or "SpectralCard" => MotelyFilterItemType.SpectralCard,
        "Boss" or "BossBlind" => MotelyFilterItemType.Boss,
        "Tag" => MotelyFilterItemType.SmallBlindTag,
        "SmallBlindTag" => MotelyFilterItemType.SmallBlindTag,
        "BigBlindTag" => MotelyFilterItemType.BigBlindTag,
        "StandardCard" => MotelyFilterItemType.PlayingCard,
        "Event" => MotelyFilterItemType.Event,
        "ErraticRank" => MotelyFilterItemType.ErraticRank,
        "GrosMichelExtinct" => MotelyFilterItemType.GrosMichelExtinct,
        "StartingDraw" => MotelyFilterItemType.StartingDraw,
        _ => throw new NotSupportedException($"Unknown clause type: {type}"),
    };

    // ── Helpers ──

    private static T? ParseEnum<T>(string? value) where T : struct, Enum
        => value != null && Enum.TryParse<T>(value, true, out var result) ? result : null;

}

public sealed class JokerSource
{
    public JokerSourceType Source { get; set; }
    public int[] Indices { get; set; } = [];
}

public enum JokerSourceType
{
    Shop,
    BoosterPack,
    Judgement,
    Wraith,
    RiffRaff,
    RareTag,
    UncommonTag
}

public sealed class JokerSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] Judgement { get; set; } = [];
    public int[] Wraith { get; set; } = [];
    public int[] RiffRaff { get; set; } = [];
    public int[] RareTag { get; set; } = [];
    public int[] UncommonTag { get; set; } = [];
}

public sealed class SoulJokerSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] SoulCard { get; set; } = [];
}

public sealed class TarotCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] Emperor { get; set; } = [];
    public int[] PurpleSealOrEightBall { get; set; } = [];
}

public sealed class SpectralCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] SixthSense { get; set; } = [];
    public int[] Seance { get; set; } = [];
}

public sealed class PlanetSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
}

public sealed class StandardCardSourceConfig
{
    public int[] ShopItems { get; set; } = [];
    public int[] BoosterPacks { get; set; } = [];
    public int[] Certificate { get; set; } = [];
    public int[] Incantation { get; set; } = [];
    public int[] Familiar { get; set; } = [];
    public int[] Grim { get; set; } = [];
    public int[] DeckDraw { get; set; } = [];
}
