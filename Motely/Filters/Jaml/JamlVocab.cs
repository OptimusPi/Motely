namespace Motely.Filters.Jaml;

/// <summary>
/// The single source of truth for JAML's grammar: which keys exist at every level, which enum
/// backs each discriminator, which source keys apply to each discriminator. Consumed by BOTH
/// <see cref="JamlConfigLoader"/> (the real parser's ValidateKeys calls) and Motely.Schema (the
/// TypeScript/VSCode generator) — one table, two consumers, so the parser and the generated
/// vocabulary can no longer independently drift from each other the way they used to.
/// </summary>
public static class JamlVocab
{
    // ── Root ──────────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<string> RootKeys =
    [
        "id", "name", "description", "author", "dateCreated", "deck", "stake", "seeds",
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
    // SharedClauseKeys applies to EVERY clause (this is what JamlConfigLoader's own
    // SharedClauseKeys enforced but JamlVocab never mirrored — with/luck/vouchers are a real,
    // working feature (JamlWith) that the old vocab silently didn't know existed).

    public static readonly IReadOnlyList<string> SharedClauseKeys =
        ["ante", "antes", "min", "max", "score", "label", "sources", "with", "luck", "vouchers"];

    /// <summary>Allowed keys inside a clause's own <c>with:</c> block (JamlWith modifiers).</summary>
    public static readonly IReadOnlyList<string> WithBlockKeys = ["luck", "vouchers"];

    /// <summary>
    /// Allowed keys inside an event clause's <c>sources:</c> block — every event discriminator
    /// (luckyMoney, wheelOfFortune, ...) shares this same shape, so it's exposed directly rather
    /// than forcing callers to pick an arbitrary discriminator key into DiscriminatorSourceKeys.
    /// </summary>
    public static readonly IReadOnlyList<string> EventSourceKeys = ["luck"];

    private static readonly string[] NoExtra      = [];
    private static readonly string[] JokerExtra   = ["edition", "stickers", "shopItems", "boosterPacks"];
    private static readonly string[] LegendExtra  = ["edition", "soulCardOnly", "soulEditionRolls", "boosterPacks"];
    private static readonly string[] StdCardExtra = ["rank", "suit", "enhancement", "seal", "edition", "shopItems", "boosterPacks"];
    private static readonly string[] DrawExtra    = ["rank", "suit"];
    private static readonly string[] WithSourcesExtra = ["shopItems", "boosterPacks"];
    private static readonly string[] RollsExtra   = ["rolls"];
    private static readonly string[] ClausesExtra = ["clauses"];
    // Event clauses share rolls/mult/value on top of SharedClauseKeys — the old vocab captured
    // none of these, so every "with 3+ rolls" / "with 2x mult" event clause read as invalid.
    private static readonly string[] EventExtra   = ["rolls", "mult", "value"];

    private static IReadOnlyList<string> Combine(params string[][] parts) =>
        [.. SharedClauseKeys, .. parts.SelectMany(p => p)];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DiscriminatorClauseKeys =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["boss"]            = Combine(NoExtra),
            ["joker"]           = Combine(JokerExtra),
            ["jokers"]          = Combine(JokerExtra),
            ["commonJoker"]     = Combine(JokerExtra),
            ["commonJokers"]    = Combine(JokerExtra),
            ["uncommonJoker"]   = Combine(JokerExtra),
            ["uncommonJokers"]  = Combine(JokerExtra),
            ["rareJoker"]       = Combine(JokerExtra),
            ["rareJokers"]      = Combine(JokerExtra),
            ["legendaryJoker"]  = Combine(LegendExtra),
            ["legendaryJokers"] = Combine(LegendExtra),
            ["voucher"]         = Combine(RollsExtra),
            ["tarotCard"]       = Combine(WithSourcesExtra),
            ["spectralCard"]    = Combine(WithSourcesExtra),
            ["planetCard"]      = Combine(WithSourcesExtra),
            ["standardCard"]    = Combine(StdCardExtra),
            ["tag"]             = Combine(RollsExtra),
            ["smallBlindTag"]   = Combine(RollsExtra),
            ["bigBlindTag"]     = Combine(RollsExtra),
            ["erraticRank"]     = Combine(NoExtra),
            ["erraticRanks"]    = Combine(NoExtra),
            ["erraticSuit"]     = Combine(NoExtra),
            ["startingDraw"]    = Combine(DrawExtra),
            ["luckyMoney"]      = Combine(EventExtra),
            ["luckyMult"]       = Combine(EventExtra),
            ["misprintMult"]    = Combine(EventExtra),
            ["wheelOfFortune"]  = Combine(EventExtra),
            ["grosMichelExtinct"]    = Combine(EventExtra),
            ["cavendishExtinct"]     = Combine(EventExtra),
            ["spaceLevelup"]         = Combine(EventExtra),
            ["businessPayout"]       = Combine(EventExtra),
            ["bloodstoneTrigger"]    = Combine(EventExtra),
            ["parkingPayout"]        = Combine(EventExtra),
            ["glassDestroy"]         = Combine(EventExtra),
            ["wheelStaysFlipped"]    = Combine(EventExtra),
            ["and"]             = Combine(ClausesExtra),
            ["or"]              = Combine(ClausesExtra),
        };

    // ── Per-discriminator: allowed source keys ────────────────────────────────
    // "emperor" is deliberately absent from joker sources: JokerSourceConfig has no Emperor
    // field, so the old loader accepted it, validated it, and silently dropped it on the floor.
    // requireMega/requireMegaPack are both real aliases the loader accepts for the same bool —
    // the old vocab only knew one of the two.

    private static readonly string[] JokerSrc     = ["shopItems", "boosterPacks", "judgement", "wraith", "riffRaff", "rareTag", "uncommonTag", "commonShopJokers", "uncommonShopJokers", "rareShopJokers", "allShopJokers"];
    private static readonly string[] LegendarySrc = ["boosterPacks", "arcanaPacks", "spectralPacks", "soulCard", "requireMega", "requireMegaPack"];
    private static readonly string[] TarotSrc     = ["shopItems", "boosterPacks", "emperor", "purpleSealOrEightBall", "charmTag"];
    private static readonly string[] SpectralSrc  = ["shopItems", "boosterPacks", "sixthSense", "seance", "etherealTag", "requireMega", "requireMegaPack"];
    private static readonly string[] PlanetSrc    = ["shopItems", "boosterPacks"];
    private static readonly string[] StdCardSrc   = ["shopItems", "boosterPacks", "certificate", "incantation", "familiar", "grim", "deckDraw"];
    private static readonly string[] EventSrc     = ["luck"];

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
