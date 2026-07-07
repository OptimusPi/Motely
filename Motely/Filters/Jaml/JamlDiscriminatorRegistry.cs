using System.Diagnostics.CodeAnalysis;

namespace Motely.Filters.Jaml;

/// <summary>
/// The ONLY place JAML discriminator strings are enumerated. Maps each discriminator to its
/// concrete clause type (whose static ClauseKeys field is the real key list) and, where
/// applicable, its source-config type (whose static SourceKeys field is the real source-key
/// list) and the enum type backing its scalar value. JamlConfigLoader (real parsing/validation)
/// and Motely.Schema (jaml-lang/jaml-lsp/jaml-codemirror generation) both read this same table —
/// replaces JamlVocab.Discriminators, JamlVocab.DiscriminatorValueEnum, the loader's own
/// AllDiscriminatorKeys(), and the loader's ClauseKeys(string) switch, all at once.
/// </summary>
public sealed record JamlDiscriminatorEntry(
    [property: DynamicallyAccessedMembers(JamlDiscriminatorRegistry.ClauseReflectionShape)]
    [param: DynamicallyAccessedMembers(JamlDiscriminatorRegistry.ClauseReflectionShape)]
    Type ClauseType,
    [property: DynamicallyAccessedMembers(JamlDiscriminatorRegistry.ClauseReflectionShape)]
    [param: DynamicallyAccessedMembers(JamlDiscriminatorRegistry.ClauseReflectionShape)]
    Type? SourceConfigType,
    Type? ValueEnumType,
    bool RollsAreInlineValue = false,
    int[]? RollsDefault = null
);

public static class JamlDiscriminatorRegistry
{
    // The one set of reflection capabilities every clause/source-config type in this registry
    // needs to survive NativeAOT trimming: a public parameterless constructor
    // (Activator.CreateInstance), public instance properties (GetProperty for
    // With/Sources/Jokers/IsWildcard/generic scalars), and public static fields (GetField for the
    // ClauseKeys/SourceKeys markers). Every entry below is built from a compile-time
    // `typeof(ConcreteType)` literal, so annotating ClauseType/SourceConfigType with this once
    // (on the record above) is what lets the trimmer statically verify — not just tolerate —
    // every downstream Activator.CreateInstance/GetProperty/GetField call in
    // JamlClausePopulator and JamlConfigWriter that flows through entry.ClauseType or
    // entry.SourceConfigType.
    public const DynamicallyAccessedMemberTypes ClauseReflectionShape =
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    public static readonly IReadOnlyDictionary<string, JamlDiscriminatorEntry> Entries =
        new Dictionary<string, JamlDiscriminatorEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["boss"]            = new(typeof(BossClause), null, typeof(MotelyBossBlind)),
            ["joker"]           = new(typeof(JokerClause), typeof(JokerSourceConfig), typeof(MotelyJoker)),
            ["jokers"]          = new(typeof(JokerClause), typeof(JokerSourceConfig), typeof(MotelyJoker)),
            ["commonJoker"]     = new(typeof(CommonJokerClause), typeof(JokerSourceConfig), typeof(MotelyJokerCommon)),
            ["commonJokers"]    = new(typeof(CommonJokerClause), typeof(JokerSourceConfig), typeof(MotelyJokerCommon)),
            ["uncommonJoker"]   = new(typeof(UncommonJokerClause), typeof(JokerSourceConfig), typeof(MotelyJokerUncommon)),
            ["uncommonJokers"]  = new(typeof(UncommonJokerClause), typeof(JokerSourceConfig), typeof(MotelyJokerUncommon)),
            ["rareJoker"]       = new(typeof(RareJokerClause), typeof(JokerSourceConfig), typeof(MotelyJokerRare)),
            ["rareJokers"]      = new(typeof(RareJokerClause), typeof(JokerSourceConfig), typeof(MotelyJokerRare)),
            ["legendaryJoker"]  = new(typeof(LegendaryJokerClause), typeof(LegendaryJokerSourceConfig), typeof(MotelyJoker)),
            ["legendaryJokers"] = new(typeof(LegendaryJokerClause), typeof(LegendaryJokerSourceConfig), typeof(MotelyJoker)),
            ["voucher"]         = new(typeof(VoucherClause), null, typeof(MotelyVoucher), RollsDefault: [0]),
            ["tarotCard"]       = new(typeof(TarotCardClause), typeof(TarotCardSourceConfig), typeof(MotelyTarotCard)),
            ["spectralCard"]    = new(typeof(SpectralCardClause), typeof(SpectralCardSourceConfig), typeof(MotelySpectralCard)),
            ["planetCard"]      = new(typeof(PlanetCardClause), typeof(PlanetSourceConfig), typeof(MotelyPlanetCard)),
            ["standardCard"]    = new(typeof(StandardCardClause), typeof(StandardCardSourceConfig), null),
            ["tag"]             = new(typeof(TagClause), null, typeof(MotelyTag), RollsDefault: [0, 1]),
            ["smallBlindTag"]   = new(typeof(TagClause), null, typeof(MotelyTag), RollsDefault: [0]),
            ["bigBlindTag"]     = new(typeof(TagClause), null, typeof(MotelyTag), RollsDefault: [1]),
            ["erraticRank"]     = new(typeof(ErraticRankClause), null, typeof(MotelyStandardcardRank)),
            ["erraticRanks"]    = new(typeof(ErraticRankClause), null, typeof(MotelyStandardcardRank)),
            ["erraticSuit"]     = new(typeof(ErraticSuitClause), null, typeof(MotelyStandardcardSuit)),
            ["startingDraw"]    = new(typeof(StartingDrawClause), null, null),
            ["luckyMoney"]      = new(typeof(LuckyMoneyClause), null, null, RollsAreInlineValue: true),
            ["luckyMult"]       = new(typeof(LuckyMultClause), null, null, RollsAreInlineValue: true),
            ["misprintMult"]    = new(typeof(MisprintMultClause), null, null, RollsAreInlineValue: true),
            ["wheelOfFortune"]  = new(typeof(WheelOfFortuneClause), null, null, RollsAreInlineValue: true),
            ["grosMichelExtinct"] = new(typeof(GrosMichelExtinctClause), null, null, RollsAreInlineValue: true),
            ["cavendishExtinct"]  = new(typeof(CavendishExtinctClause), null, null, RollsAreInlineValue: true),
            ["spaceLevelup"]      = new(typeof(SpaceLevelupClause), null, null, RollsAreInlineValue: true),
            ["businessPayout"]    = new(typeof(BusinessPayoutClause), null, null, RollsAreInlineValue: true),
            ["bloodstoneTrigger"] = new(typeof(BloodstoneTriggerClause), null, null, RollsAreInlineValue: true),
            ["parkingPayout"]     = new(typeof(ParkingPayoutClause), null, null, RollsAreInlineValue: true),
            ["glassDestroy"]      = new(typeof(GlassDestroyClause), null, null, RollsAreInlineValue: true),
            ["wheelStaysFlipped"] = new(typeof(Motely.Filters.Jaml.WheelStaysFlippedClause), null, null, RollsAreInlineValue: true),
            ["and"]             = new(typeof(Motely.Filters.AndClause), null, null),
            ["or"]              = new(typeof(Motely.Filters.OrClause), null, null),
        };

    /// <summary>Reads a Type's public static readonly string[] field by name via reflection —
    /// the actual mechanism that lets JamlConfigLoader and Motely.Schema read ClauseKeys/
    /// SourceKeys off arbitrary clause/source types without each caller needing its own switch.</summary>
    public static string[] StaticStringArrayField(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        string fieldName
    )
    {
        // FlattenHierarchy: AndClause/OrClause declare no ClauseKeys of their own — they inherit
        // it from LogicClause. Without this flag, GetField only sees a type's own members.
        var field = type.GetField(
            fieldName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy
        );
        return field?.GetValue(null) as string[] ?? [];
    }

    public static string[] ClauseKeysFor(string discriminator) =>
        Entries.TryGetValue(discriminator, out var entry)
            ? StaticStringArrayField(entry.ClauseType, "ClauseKeys")
            : [];

    public static string[] SourceKeysFor(string discriminator) =>
        Entries.TryGetValue(discriminator, out var entry) && entry.SourceConfigType is { } sourceType
            ? StaticStringArrayField(sourceType, "SourceKeys")
            : [];
}
