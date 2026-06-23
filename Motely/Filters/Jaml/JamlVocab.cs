namespace Motely.Filters.Jaml;

/// <summary>
/// Static vocabulary tables consumed by Motely.Schema (the TypeScript/VSCode generator)
/// and by jaml-lang (the editor language service).
/// Single source of truth for: which keys exist, which enum backs each discriminator,
/// which source keys apply to each discriminator.
/// </summary>
public static class JamlVocab
{
    // ── Root ──────────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<string> RootKeys =
    [
        "id", "name", "description", "author", "deck", "stake", "seeds",
        "must", "should", "mustNot",
    ];

    // ── Discriminators ────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<string> Discriminators =
    [
        "boss",
        "joker", "jokers",
        "commonJoker", "commonJokers", "uncommonJoker", "uncommonJokers",
        "rareJoker", "rareJokers", "legendaryJoker", "legendaryJokers",
        "voucher",
        "tarotCard", "spectralCard", "planetCard", "standardCard",
        "tag", "smallBlindTag", "bigBlindTag",
        "luckyMoney", "luckyMult", "misprintMult",
        "wheelOfFortune", "grosMichelExtinct", "cavendishExtinct",
        "spaceLevelup", "businessPayout", "bloodstoneTrigger",
        "parkingPayout", "glassDestroy", "wheelStaysFlipped",
        "startingDraw",
        "erraticRank", "erraticRanks", "erraticSuit",
        "and", "or",
    ];

    // ── Per-discriminator: value enum ─────────────────────────────────────────
    // What enum type is valid as the scalar value of this discriminator.
    // null = no scalar value (logic clauses, event clauses with array value).

    public static readonly IReadOnlyDictionary<string, string?> DiscriminatorValueEnum =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["boss"]            = "MotelyBossBlind",
            ["joker"]           = "MotelyJoker",
            ["jokers"]          = "MotelyJoker",
            ["commonJoker"]     = "MotelyJokerCommon",
            ["commonJokers"]    = "MotelyJokerCommon",
            ["uncommonJoker"]   = "MotelyJokerUncommon",
            ["uncommonJokers"]  = "MotelyJokerUncommon",
            ["rareJoker"]       = "MotelyJokerRare",
            ["rareJokers"]      = "MotelyJokerRare",
            ["legendaryJoker"]  = "MotelyJoker",
            ["legendaryJokers"] = "MotelyJoker",
            ["voucher"]         = "MotelyVoucher",
            ["tarotCard"]       = "MotelyTarotCard",
            ["spectralCard"]    = "MotelySpectralCard",
            ["planetCard"]      = "MotelyPlanetCard",
            ["standardCard"]    = null,
            ["tag"]             = "MotelyTag",
            ["smallBlindTag"]   = "MotelyTag",
            ["bigBlindTag"]     = "MotelyTag",
            ["erraticRank"]     = "MotelyStandardcardRank",
            ["erraticRanks"]    = "MotelyStandardcardRank",
            ["erraticSuit"]     = "MotelyStandardcardSuit",
            ["luckyMoney"]      = null,
            ["luckyMult"]       = null,
            ["misprintMult"]    = null,
            ["wheelOfFortune"]  = null,
            ["grosMichelExtinct"]   = null,
            ["cavendishExtinct"]    = null,
            ["spaceLevelup"]        = null,
            ["businessPayout"]      = null,
            ["bloodstoneTrigger"]   = null,
            ["parkingPayout"]       = null,
            ["glassDestroy"]        = null,
            ["wheelStaysFlipped"]   = null,
            ["startingDraw"]        = null,
            ["and"]             = null,
            ["or"]              = null,
        };

    // ── Per-discriminator: allowed clause-level keys ──────────────────────────

    private static readonly string[] Base        = ["ante", "antes", "min", "max", "score", "label"];
    private static readonly string[] WithSources = [.. Base, "shopItems", "sources"];
    private static readonly string[] JokerKeys   = [.. Base, "edition", "stickers", "shopItems", "sources"];
    private static readonly string[] LegendKeys  = [.. Base, "edition", "soulCardOnly", "soulEditionRolls", "sources"];
    private static readonly string[] StdCardKeys = [.. Base, "rank", "suit", "enhancement", "seal", "edition", "shopItems", "sources"];
    private static readonly string[] DrawKeys    = [.. Base, "rank", "suit"];
    private static readonly string[] MisprintKeys = [.. Base, "value", "sources"];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DiscriminatorClauseKeys =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["boss"]            = Base,
            ["joker"]           = JokerKeys,
            ["jokers"]          = JokerKeys,
            ["commonJoker"]     = JokerKeys,
            ["commonJokers"]    = JokerKeys,
            ["uncommonJoker"]   = JokerKeys,
            ["uncommonJokers"]  = JokerKeys,
            ["rareJoker"]       = JokerKeys,
            ["rareJokers"]      = JokerKeys,
            ["legendaryJoker"]  = LegendKeys,
            ["legendaryJokers"] = LegendKeys,
            ["voucher"]         = Base,
            ["tarotCard"]       = WithSources,
            ["spectralCard"]    = WithSources,
            ["planetCard"]      = WithSources,
            ["standardCard"]    = StdCardKeys,
            ["tag"]             = Base,
            ["smallBlindTag"]   = Base,
            ["bigBlindTag"]     = Base,
            ["erraticRank"]     = Base,
            ["erraticRanks"]    = Base,
            ["erraticSuit"]     = Base,
            ["startingDraw"]    = DrawKeys,
            ["luckyMoney"]      = [.. Base, "sources"],
            ["luckyMult"]       = [.. Base, "sources"],
            ["misprintMult"]    = MisprintKeys,
            ["wheelOfFortune"]  = [.. Base, "sources"],
            ["grosMichelExtinct"]    = [.. Base, "sources"],
            ["cavendishExtinct"]     = [.. Base, "sources"],
            ["spaceLevelup"]         = [.. Base, "sources"],
            ["businessPayout"]       = [.. Base, "sources"],
            ["bloodstoneTrigger"]    = [.. Base, "sources"],
            ["parkingPayout"]        = [.. Base, "sources"],
            ["glassDestroy"]         = [.. Base, "sources"],
            ["wheelStaysFlipped"]    = Base,
            ["and"]             = Base,
            ["or"]              = Base,
        };

    // ── Per-discriminator: allowed source keys ────────────────────────────────

    private static readonly string[] JokerSrc    = ["shopItems", "boosterPacks", "judgement", "wraith", "riffRaff", "rareTag", "uncommonTag", "commonShopJokers", "uncommonShopJokers", "rareShopJokers", "allShopJokers"];
    private static readonly string[] LegendarySrc = ["shopItems", "boosterPacks", "arcanaPacks", "spectralPacks", "soulCard", "requireMega"];
    private static readonly string[] TarotSrc    = ["shopItems", "boosterPacks", "emperor", "purpleSealOrEightBall", "charmTag"];
    private static readonly string[] SpectralSrc = ["shopItems", "boosterPacks", "sixthSense", "seance", "etherealTag"];
    private static readonly string[] PlanetSrc   = ["shopItems", "boosterPacks"];
    private static readonly string[] StdCardSrc  = ["shopItems", "boosterPacks", "certificate", "incantation", "familiar", "grim", "deckDraw"];
    private static readonly string[] EventSrc    = ["luck"];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DiscriminatorSourceKeys =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["joker"]           = JokerSrc,
            ["jokers"]          = JokerSrc,
            ["commonJoker"]     = JokerSrc,
            ["commonJokers"]    = JokerSrc,
            ["uncommonJoker"]   = JokerSrc,
            ["uncommonJokers"]  = JokerSrc,
            ["rareJoker"]       = JokerSrc,
            ["rareJokers"]      = JokerSrc,
            ["legendaryJoker"]  = LegendarySrc,
            ["legendaryJokers"] = LegendarySrc,
            ["tarotCard"]       = TarotSrc,
            ["spectralCard"]    = SpectralSrc,
            ["planetCard"]      = PlanetSrc,
            ["standardCard"]    = StdCardSrc,
            ["luckyMoney"]      = EventSrc,
            ["luckyMult"]       = EventSrc,
            ["misprintMult"]    = EventSrc,
            ["wheelOfFortune"]  = EventSrc,
            ["grosMichelExtinct"]    = EventSrc,
            ["cavendishExtinct"]     = EventSrc,
            ["spaceLevelup"]         = EventSrc,
            ["businessPayout"]       = EventSrc,
            ["bloodstoneTrigger"]    = EventSrc,
            ["parkingPayout"]        = EventSrc,
            ["glassDestroy"]         = EventSrc,
        };

    // ── Per-clause-key: value enum ────────────────────────────────────────────
    // Clause-level keys whose value is constrained to an enum. This is the single
    // source of truth that value-type generators (JSON schema, hover, completion)
    // must read — so none of them hand-maintains its own copy and drifts. That
    // drift is exactly how the JSON-schema generator once mapped `suit` to its enum
    // but left `rank` (right next to it) as a free string. Array-valued keys
    // (`stickers`) constrain each element to the named enum.

    public static readonly IReadOnlyDictionary<string, string> ClauseKeyValueEnum =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["edition"]     = "MotelyItemEdition",
            ["enhancement"] = "MotelyItemEnhancement",
            ["seal"]        = "MotelyItemSeal",
            ["suit"]        = "MotelyStandardcardSuit",
            ["rank"]        = "MotelyStandardcardRank",
            ["stickers"]    = "MotelyJokerSticker",
        };

    // ── Enum member lists (from the actual C# enums via reflection) ───────────

    public static IReadOnlyDictionary<string, string[]> GetAllEnums() =>
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["MotelyJoker"]             = Enum.GetNames<MotelyJoker>(),
            ["MotelyJokerCommon"]       = Enum.GetNames<MotelyJokerCommon>(),
            ["MotelyJokerUncommon"]     = Enum.GetNames<MotelyJokerUncommon>(),
            ["MotelyJokerRare"]         = Enum.GetNames<MotelyJokerRare>(),
            ["MotelyBossBlind"]         = Enum.GetNames<MotelyBossBlind>(),
            ["MotelyVoucher"]           = Enum.GetNames<MotelyVoucher>(),
            ["MotelyTarotCard"]         = Enum.GetNames<MotelyTarotCard>(),
            ["MotelySpectralCard"]      = Enum.GetNames<MotelySpectralCard>(),
            ["MotelyPlanetCard"]        = Enum.GetNames<MotelyPlanetCard>(),
            ["MotelyTag"]               = Enum.GetNames<MotelyTag>(),
            ["MotelyDeck"]              = Enum.GetNames<MotelyDeck>(),
            ["MotelyStake"]             = Enum.GetNames<MotelyStake>(),
            ["MotelyStandardcardRank"]  = Enum.GetNames<MotelyStandardcardRank>(),
            ["MotelyStandardcardSuit"]  = Enum.GetNames<MotelyStandardcardSuit>(),
            ["MotelyItemEdition"]       = Enum.GetNames<MotelyItemEdition>(),
            ["MotelyItemEnhancement"]   = Enum.GetNames<MotelyItemEnhancement>(),
            ["MotelyItemSeal"]          = Enum.GetNames<MotelyItemSeal>(),
            ["MotelyJokerSticker"]      = Enum.GetNames<MotelyJokerSticker>(),
        };
}
