using System.Diagnostics;

namespace Motely.Filters;

/// <summary>
/// Fully validated, typed runtime configuration for seed filtering.
/// NO NULLABLE FIELDS - if this object exists, it's guaranteed runnable.
///
/// Flow: JAML Text → MotelyJamlConfig (DTO) → Validate → MotelyRunConfig (this)
/// </summary>
public sealed class MotelyRunConfig
{
    // Metadata (all guaranteed non-null after construction)
    public string Name { get; }
    public string Author { get; }
    public string Description { get; }
    public string Deck { get; }
    public string Stake { get; }
    public MotelyScoreAggregationMode ScoreAggregationMode { get; }

    // Filter clauses - arrays, never null, may be empty
    public MotelyRunClause[] Must { get; }
    public MotelyRunClause[] Should { get; }
    public MotelyRunClause[] MustNot { get; }

    // Pre-partitioned for performance (computed once at construction)
    public MotelyRunClause[] MustVouchers { get; }
    public MotelyRunClause[] MustNonVouchers { get; }
    public MotelyRunClause[] ShouldVouchers { get; }
    public MotelyRunClause[] ShouldNonVouchers { get; }

    // Pre-computed expensive calculations
    public int MaxVoucherAnte { get; }
    public int MaxBossAnte { get; }

    // Defaults for clauses that don't specify their own
    public MotelyFilterDefaults Defaults { get; }

    public MotelyRunConfig(
        string name,
        string author,
        string description,
        string deck,
        string stake,
        MotelyScoreAggregationMode scoreAggregationMode,
        MotelyRunClause[] must,
        MotelyRunClause[] should,
        MotelyRunClause[] mustNot,
        MotelyFilterDefaults defaults
    )
    {
        Name = name;
        Author = author;
        Description = description;
        Deck = deck;
        Stake = stake;
        ScoreAggregationMode = scoreAggregationMode;
        Defaults = defaults;

        Must = must ?? [];
        Should = should ?? [];
        MustNot = mustNot ?? [];

        // Partition clauses by type for performance
        (MustVouchers, MustNonVouchers) = PartitionByVoucher(Must);
        (ShouldVouchers, ShouldNonVouchers) = PartitionByVoucher(Should);

        // Compute max antes
        MaxVoucherAnte = ComputeMaxAnte(MustVouchers, ShouldVouchers);
        MaxBossAnte = ComputeMaxBossAnte(Must, Should);
    }

    private static (MotelyRunClause[] vouchers, MotelyRunClause[] nonVouchers) PartitionByVoucher(
        MotelyRunClause[] clauses
    )
    {
        int voucherCount = 0;
        foreach (var c in clauses)
            if (c.ItemType == MotelyFilterItemType.Voucher)
                voucherCount++;

        var vouchers = new MotelyRunClause[voucherCount];
        var nonVouchers = new MotelyRunClause[clauses.Length - voucherCount];
        int vi = 0,
            nvi = 0;

        foreach (var c in clauses)
        {
            if (c.ItemType == MotelyFilterItemType.Voucher)
                vouchers[vi++] = c;
            else
                nonVouchers[nvi++] = c;
        }

        return (vouchers, nonVouchers);
    }

    private static int ComputeMaxAnte(
        MotelyRunClause[] mustVouchers,
        MotelyRunClause[] shouldVouchers
    )
    {
        int max = 0;
        foreach (var c in mustVouchers)
            if (c.Antes.Length > 0)
                max = Math.Max(max, c.Antes.Max());
        foreach (var c in shouldVouchers)
            if (c.Antes.Length > 0)
                max = Math.Max(max, c.Antes.Max());
        return max;
    }

    private static int ComputeMaxBossAnte(MotelyRunClause[] must, MotelyRunClause[] should)
    {
        int max = 0;
        foreach (var c in must)
            if (c.ItemType == MotelyFilterItemType.Boss && c.Antes.Length > 0)
                max = Math.Max(max, c.Antes.Max());
        foreach (var c in should)
            if (c.ItemType == MotelyFilterItemType.Boss && c.Antes.Length > 0)
                max = Math.Max(max, c.Antes.Max());
        return max;
    }
}

/// <summary>
/// Fully typed filter clause - NO STRINGS for type identification.
/// All enum fields are non-nullable; use sentinel values or check ItemType to know which apply.
/// </summary>
public sealed class MotelyRunClause
{
    // Core identification - TYPED, not string!
    public MotelyFilterItemType ItemType { get; }

    // Scope
    public int[] Antes { get; }
    public int Score { get; }
    public bool IsInverted { get; }

    // Label for output columns
    public string Label { get; }

    // Item-specific typed values (check ItemType to know which is valid)
    public MotelyJoker Joker { get; }
    public MotelyJoker[] Jokers { get; }
    public MotelyVoucher Voucher { get; }
    public MotelyVoucher[] Vouchers { get; }
    public MotelyTarotCard Tarot { get; }
    public MotelyTarotCard[] Tarots { get; }
    public MotelyPlanetCard Planet { get; }
    public MotelyPlanetCard[] Planets { get; }
    public MotelySpectralCard Spectral { get; }
    public MotelySpectralCard[] Spectrals { get; }
    public MotelyTag Tag { get; }
    public MotelyTag[] Tags { get; }
    public MotelyTagType TagType { get; }
    public MotelyBossBlind Boss { get; }
    public MotelyBossBlind[] Bosses { get; }
    public MotelyEventType EventType { get; }

    // Card properties
    public MotelyPlayingCardSuit Suit { get; }
    public MotelyPlayingCardRank Rank { get; }
    public MotelyItemSeal Seal { get; }
    public MotelyItemEnhancement Enhancement { get; }
    public MotelyItemEdition Edition { get; }
    public MotelyJokerSticker[] Stickers { get; }

    // Wildcard support
    public bool IsWildcard { get; }
    public MotelyJsonConfigWildcards Wildcard { get; }

    // Sources - typed, never null
    public MotelyRunSources Sources { get; }

    // Nested clauses for And/Or
    public MotelyRunClause[] NestedClauses { get; }

    // Event-specific
    public int[] Rolls { get; }
    public int? Min { get; }

    public MotelyRunClause(
        MotelyFilterItemType itemType,
        int[] antes,
        int score,
        bool isInverted,
        string label,
        MotelyJoker joker = default,
        MotelyJoker[]? jokers = null,
        MotelyVoucher voucher = default,
        MotelyVoucher[]? vouchers = null,
        MotelyTarotCard tarot = default,
        MotelyTarotCard[]? tarots = null,
        MotelyPlanetCard planet = default,
        MotelyPlanetCard[]? planets = null,
        MotelySpectralCard spectral = default,
        MotelySpectralCard[]? spectrals = null,
        MotelyTag tag = default,
        MotelyTag[]? tags = null,
        MotelyTagType tagType = default,
        MotelyBossBlind boss = default,
        MotelyBossBlind[]? bosses = null,
        MotelyEventType eventType = default,
        MotelyPlayingCardSuit suit = default,
        MotelyPlayingCardRank rank = default,
        MotelyItemSeal seal = default,
        MotelyItemEnhancement enhancement = default,
        MotelyItemEdition edition = default,
        MotelyJokerSticker[]? stickers = null,
        bool isWildcard = false,
        MotelyJsonConfigWildcards wildcard = default,
        MotelyRunSources? sources = null,
        MotelyRunClause[]? nestedClauses = null,
        int[]? rolls = null,
        int? min = null
    )
    {
        ItemType = itemType;
        Antes = antes;
        Score = score;
        IsInverted = isInverted;
        Label = label;

        Joker = joker;
        Jokers = jokers ?? [];
        Voucher = voucher;
        Vouchers = vouchers ?? [];
        Tarot = tarot;
        Tarots = tarots ?? [];
        Planet = planet;
        Planets = planets ?? [];
        Spectral = spectral;
        Spectrals = spectrals ?? [];
        Tag = tag;
        Tags = tags ?? [];
        TagType = tagType;
        Boss = boss;
        Bosses = bosses ?? [];
        EventType = eventType;

        Suit = suit;
        Rank = rank;
        Seal = seal;
        Enhancement = enhancement;
        Edition = edition;
        Stickers = stickers ?? [];

        IsWildcard = isWildcard;
        Wildcard = wildcard;

        Sources = sources ?? MotelyRunSources.Default;
        NestedClauses = nestedClauses ?? [];
        Rolls = rolls ?? [];
        Min = min;
    }
}

/// <summary>
/// Typed sources configuration - no nullables
/// </summary>
public sealed class MotelyRunSources
{
    public int[] PackSlots { get; }
    public int[] ShopSlots { get; }
    public bool Tags { get; }
    public bool RequireMega { get; }

    // Pre-computed min/max for fast filtering
    public int MinPackSlot { get; }
    public int MaxPackSlot { get; }
    public int MinShopSlot { get; }
    public int MaxShopSlot { get; }

    public static readonly MotelyRunSources Default = new(
        packSlots: [0, 1, 2, 3],
        shopSlots: [0, 1, 2, 3],
        tags: true,
        requireMega: false
    );

    public static readonly MotelyRunSources Empty = new(
        packSlots: [],
        shopSlots: [],
        tags: false,
        requireMega: false
    );

    public MotelyRunSources(int[] packSlots, int[] shopSlots, bool tags, bool requireMega)
    {
        PackSlots = packSlots;
        ShopSlots = shopSlots;
        Tags = tags;
        RequireMega = requireMega;

        MinPackSlot = PackSlots.Length > 0 ? PackSlots.Min() : 0;
        MaxPackSlot = PackSlots.Length > 0 ? PackSlots.Max() : -1;
        MinShopSlot = ShopSlots.Length > 0 ? ShopSlots.Min() : 0;
        MaxShopSlot = ShopSlots.Length > 0 ? ShopSlots.Max() : -1;
    }
}
