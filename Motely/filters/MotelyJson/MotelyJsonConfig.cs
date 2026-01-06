using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using System.Numerics;

namespace Motely.Filters;

/// <summary>
/// Constants for slot limits in Balatro
/// </summary>
internal static class MotelySlotLimits
{
    /// <summary>Pack slots: 0-5 (6 total max in ante 2+, 4 in ante 1)</summary>
    public const int MAX_PACK_SLOT = 5;

    /// <summary>Shop slots: theoretically unlimited (player can reroll), capped at 1024 for array size</summary>
    public const int MAX_SHOP_SLOT = 1023;
}

/// <summary>
/// User-configurable defaults for filter clauses (specified in JAML/JSON config)
/// </summary>
public class MotelyFilterDefaults
{
    [JsonPropertyName("antes")]
    public int[]? Antes { get; set; }

    [JsonPropertyName("packSlots")]
    [YamlMember(Alias = "packSlots")]
    public int[]? PackSlots { get; set; }

    [JsonPropertyName("shopSlots")]
    [YamlMember(Alias = "shopSlots")]
    public int[]? ShopSlots { get; set; }

    [JsonPropertyName("score")]
    public int? Score { get; set; }

    // Fallback hardcoded defaults if user doesn't specify
    [JsonIgnore]
    public static readonly int[] DEFAULT_ANTES = [1, 2, 3, 4, 5, 6, 7, 8];

    [JsonIgnore]
    public static readonly int[] DEFAULT_PACK_SLOTS_ANTE_1 = [0, 1, 2, 3];

    [JsonIgnore]
    public static readonly int[] DEFAULT_PACK_SLOTS_ANTE_2_PLUS = [0, 1, 2, 3, 4, 5];

    [JsonIgnore]
    public static readonly int[] DEFAULT_SHOP_SLOTS_ANTE_1 = [0, 1, 2, 3];

    [JsonIgnore]
    public static readonly int[] DEFAULT_SHOP_SLOTS_ANTE_2_PLUS = [0, 1, 2, 3, 4, 5];

    [JsonIgnore]
    public const int DEFAULT_SCORE = 1;

    /// <summary>
    /// Get effective antes (user-specified or hardcoded default)
    /// </summary>
    public int[] GetEffectiveAntes() => Antes ?? DEFAULT_ANTES;

    /// <summary>
    /// Get effective pack slots for a given ante (handles ante 1 vs ante 2+ differences)
    /// </summary>
    public int[] GetEffectivePackSlots(int ante)
    {
        if (PackSlots != null)
        {
            // User specified pack slots - filter based on ante (zero-allocation)
            if (ante == 1)
            {
                int count = 0;
                foreach (var slot in PackSlots)
                    if (slot <= 3) count++;
                
                if (count == 0) return [];
                
                int[] result = new int[count];
                int index = 0;
                foreach (var slot in PackSlots)
                    if (slot <= 3) result[index++] = slot;
                
                return result;
            }
            return PackSlots;
        }

        // Use hardcoded defaults
        return ante == 1 ? DEFAULT_PACK_SLOTS_ANTE_1 : DEFAULT_PACK_SLOTS_ANTE_2_PLUS;
    }

    /// <summary>
    /// Get effective shop slots for a given ante (handles ante 1 vs ante 2+ differences)
    /// </summary>
    public int[] GetEffectiveShopSlots(int ante)
    {
        if (ShopSlots != null)
        {
            // User specified shop slots - filter based on ante (zero-allocation)
            if (ante == 1)
            {
                int count = 0;
                foreach (var slot in ShopSlots)
                    if (slot <= 3) count++;
                
                if (count == 0) return [];
                
                int[] result = new int[count];
                int index = 0;
                foreach (var slot in ShopSlots)
                    if (slot <= 3) result[index++] = slot;
                
                return result;
            }
            return ShopSlots;
        }

        // Use hardcoded defaults
        return ante == 1 ? DEFAULT_SHOP_SLOTS_ANTE_1 : DEFAULT_SHOP_SLOTS_ANTE_2_PLUS;
    }

    /// <summary>
    /// Get effective score (user-specified or hardcoded default)
    /// </summary>
    public int GetEffectiveScore() => Score ?? DEFAULT_SCORE;
}

/// <summary>
/// Wildcard types for joker and card filtering
/// </summary>
public enum MotelyJsonConfigWildcards
{
    AnyJoker,
    AnyCommon,
    AnyUncommon,
    AnyRare,
    AnyLegendary,
    AnyTarot,
    AnySpectral,
    AnyPlanet,
}

/// <summary>
/// Top-level score aggregation mode for SHOULD clauses
/// </summary>
public enum MotelyScoreAggregationMode
{
    /// <summary>
    /// Sum of (count * score) across all SHOULD clauses (default)
    /// </summary>
    Sum,

    /// <summary>
    /// Use max raw occurrence count across SHOULD clauses (ignores per-clause score)
    /// </summary>
    MaxCount,
}

/// <summary>
/// MongoDB compound Operator-style JSON configuration
/// </summary>
    /// <summary>
    /// MongoDB compound Operator-style JSON configuration for Balatro seed filters.
    /// 
    /// NOTE: There is a naming inconsistency in this codebase:
    /// - Class name: MotelyJsonConfig (correct spelling: "Motely")
    /// - Nested class: MotelyJsonFilterClause (typo: "Motely" instead of "Motely")
    /// This typo is preserved for backwards compatibility and to avoid breaking changes.
    /// </summary>
    public class MotelyJsonConfig
    {
        // Metadata fields
    [JsonPropertyName("name")]
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [JsonPropertyName("author")]
    [YamlMember(Alias = "author")]
    public string? Author { get; set; }

    [JsonPropertyName("description")]
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [JsonPropertyName("dateCreated")]
    public DateTime? DateCreated { get; set; }

    [JsonPropertyName("verifiedSeed")]
    public string? VerifiedSeed { get; set; }

    [JsonPropertyName("deck")]
    public string? Deck { get; set; } = "Red";

    [JsonPropertyName("stake")]
    public string? Stake { get; set; } = "White";

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("defaults")]
    public MotelyFilterDefaults? Defaults { get; set; }

    [JsonIgnore]
    [YamlIgnore]
    public MotelyScoreAggregationMode ScoreAggregationMode { get; private set; } =
        MotelyScoreAggregationMode.Sum;

    [JsonPropertyName("must")]
    [YamlMember(Alias = "must")]
    public List<MotelyJsonFilterClause> Must { get; set; } = new();

    [JsonPropertyName("should")]
    [YamlMember(Alias = "should")]
    public List<MotelyJsonFilterClause> Should { get; set; } = new();

    [JsonPropertyName("mustNot")]
    [YamlMember(Alias = "mustNot")]
    public List<MotelyJsonFilterClause> MustNot { get; set; } = new();

    // PERFORMANCE: Pre-partitioned clauses to avoid repeated iteration
    [JsonIgnore]
    [YamlIgnore]
    public MotelyJsonFilterClause[] MustVouchers { get; private set; } = Array.Empty<MotelyJsonFilterClause>();

    [JsonIgnore]
    [YamlIgnore]
    public MotelyJsonFilterClause[] MustNonVouchers { get; private set; } = Array.Empty<MotelyJsonFilterClause>();

    [JsonIgnore]
    [YamlIgnore]
    public MotelyJsonFilterClause[] ShouldVouchers { get; private set; } = Array.Empty<MotelyJsonFilterClause>();

    [JsonIgnore]
    [YamlIgnore]
    public MotelyJsonFilterClause[] ShouldNonVouchers { get; private set; } = Array.Empty<MotelyJsonFilterClause>();

    public class MotelyJsonFilterClause
    {
        [JsonPropertyName("type")]
        [YamlMember(Alias = "type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("value")]
        [YamlMember(Alias = "value")]
        public string? Value { get; set; }

        [JsonPropertyName("values")]
        [YamlMember(Alias = "values")]
        public string[]? Values { get; set; }

        [JsonPropertyName("label")]
        [YamlMember(Alias = "label")]
        public string? Label { get; set; }

        [JsonPropertyName("antes")]
        [YamlMember(Alias = "antes")]
        public int[]? Antes { get; set; }

        // Nested clauses for And/Or grouping
        [JsonPropertyName("clauses")]
        [YamlMember(Alias = "clauses")]
        public List<MotelyJsonFilterClause>? Clauses { get; set; }

        // Inversion flag for mustNot clauses (set internally, not from JSON)
        [JsonIgnore]
        [YamlIgnore]
        public bool IsInverted { get; set; } = false;

        // Track whether Antes was explicitly set by user vs defaulted by ProcessClause
        // This is critical for OR/AND clause helper behavior
        [JsonIgnore]
        [YamlIgnore]
        public bool AntesWasExplicitlySet { get; set; } = false;

        [JsonPropertyName("score")]
        [YamlMember(Alias = "score")]
        public int Score { get; set; } = 1;

        [JsonPropertyName("mode")]
        [YamlMember(Alias = "mode")]
        public string? Mode { get; set; } // Per-clause scoring mode (for Or/And clauses)

        [JsonPropertyName("min")]
        [YamlMember(Alias = "min")]
        public int? Min { get; set; }

        [JsonPropertyName("filterOrder")]
        [YamlMember(Alias = "filterOrder")]
        public int? FilterOrder { get; set; } // Optional ordering for slice chain optimization

        [JsonPropertyName("edition")]
        [YamlMember(Alias = "edition")]
        public string? Edition { get; set; }

        [JsonPropertyName("stickers")]
        [YamlMember(Alias = "stickers")]
        public List<string>? Stickers { get; set; }

        // PlayingCard specific
        [JsonPropertyName("suit")]
        [YamlMember(Alias = "suit")]
        public string? Suit { get; set; }

        [JsonPropertyName("rank")]
        [YamlMember(Alias = "rank")]
        public string? Rank { get; set; }

        [JsonPropertyName("seal")]
        [YamlMember(Alias = "seal")]
        public string? Seal { get; set; }

        [JsonPropertyName("enhancement")]
        [YamlMember(Alias = "enhancement")]
        public string? Enhancement { get; set; }

        // Sources configuration
        [JsonPropertyName("sources")]
        [YamlMember(Alias = "sources")]
        public SourcesConfig? Sources { get; set; }

        // Direct properties for backwards compatibility
        [JsonPropertyName("packSlots")]
        [YamlMember(Alias = "packSlots")]
        public int[]? PackSlots { get; set; }

        [JsonPropertyName("shopSlots")]
        [YamlMember(Alias = "shopSlots")]
        public int[]? ShopSlots { get; set; }

        [JsonPropertyName("requireMega")]
        [YamlMember(Alias = "requireMega")]
        public bool? RequireMega { get; set; }

        [JsonPropertyName("tags")]
        [YamlMember(Alias = "tags")]
        public bool? Tags { get; set; }

        // Event-specific properties
        [JsonPropertyName("eventType")]
        [YamlMember(Alias = "eventType")]
        public string? EventType { get; set; }

        [JsonPropertyName("rolls")]
        [YamlMember(Alias = "rolls")]
        public int[]? Rolls { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public int[] EffectiveAntes
        {
            get => Antes ?? [];
            set { Antes = value; }
        }

        // Pre-computed values (set during ProcessClause from Sources)
        // Min/Max are calculated from Sources.min/maxShopSlot or Sources.shopSlots array
        [JsonIgnore]
        [YamlIgnore]
        public int? MinShopSlot { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public int? MaxShopSlot { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public int? MinPackSlot { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public int? MaxPackSlot { get; set; }

        // Pre-parsed enum (set during initialization, immutable after)
        [JsonIgnore]
        [YamlIgnore]
        public MotelyFilterItemType ItemTypeEnum { get; private set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyVoucher? VoucherEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyTarotCard? TarotEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyPlanetCard? PlanetEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelySpectralCard? SpectralEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyJoker? JokerEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyTag? TagEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyTagType TagTypeEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyBossBlind? BossEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyEventType? EventTypeEnum { get; set; }

        // Multi-value enum arrays for "values" property
        [JsonIgnore]
        [YamlIgnore]
        public List<MotelyJoker>? JokerEnums { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public List<MotelyVoucher>? VoucherEnums { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public List<MotelyTarotCard>? TarotEnums { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public List<MotelyPlanetCard>? PlanetEnums { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public List<MotelySpectralCard>? SpectralEnums { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public List<MotelyTag>? TagEnums { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public List<MotelyBossBlind>? BossEnums { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyItemEdition? EditionEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public List<MotelyJokerSticker>? StickerEnums { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyPlayingCardSuit? SuitEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyPlayingCardRank? RankEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyItemSeal? SealEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyItemEnhancement? EnhancementEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public MotelyJsonConfigWildcards? WildcardEnum { get; set; }

        [JsonIgnore]
        [YamlIgnore]
        public bool IsWildcard { get; set; }

        // Catch unknown properties so we can validate them
        [JsonExtensionData]
        [YamlIgnore]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        public void InitializeParsedEnums()
        {
            // PERFORMANCE FIX: Use pre-computed dictionary instead of ToLowerInvariant() + switch
            ItemTypeEnum = MotelyJsonPerformanceUtils.ParseItemType(Type);

            // Parse Value based on ItemType
            if (!string.IsNullOrEmpty(Value))
            {
                // PERFORMANCE FIX: Use pre-computed wildcard parsing
                var (isWildcard, wildcard) = MotelyJsonPerformanceUtils.ParseWildcard(Value);
                if (isWildcard)
                {
                    IsWildcard = true;
                    WildcardEnum = wildcard;
                }
                else
                {
                    // Parse specific enum values based on type
                    switch (ItemTypeEnum)
                    {
                        case MotelyFilterItemType.Joker:
                            if (MotelyEnumParser.TryParseJoker(Value, out var joker))
                            {
                                JokerEnum = joker;
                            }
                            else
                            {
                                throw new ArgumentException($"'{Value}' is not a valid Joker value.");
                            }
                            break;
                        case MotelyFilterItemType.SoulJoker:
                            if (Enum.TryParse<MotelyJoker>(Value, true, out var soulJoker))
                                JokerEnum = soulJoker;
                            break;
                        case MotelyFilterItemType.Voucher:
                            if (MotelyEnumParser.TryParseVoucher(Value, out var voucher))
                            {
                                VoucherEnum = voucher;
                            }
                            else
                            {
                                throw new ArgumentException($"'{Value}' is not a valid Voucher value.");
                            }
                            break;
                        case MotelyFilterItemType.TarotCard:
                            if (Enum.TryParse<MotelyTarotCard>(Value, true, out var tarot))
                                TarotEnum = tarot;
                            break;
                        case MotelyFilterItemType.PlanetCard:
                            if (Enum.TryParse<MotelyPlanetCard>(Value, true, out var planet))
                                PlanetEnum = planet;
                            break;
                        case MotelyFilterItemType.SpectralCard:
                            if (Enum.TryParse<MotelySpectralCard>(Value, true, out var spectral))
                                SpectralEnum = spectral;
                            break;
                        case MotelyFilterItemType.Boss:
                            if (Enum.TryParse<MotelyBossBlind>(Value, true, out var boss))
                                BossEnum = boss;
                            break;
                        case MotelyFilterItemType.ErraticRank:
                            // Parse rank from Value
                            if (Enum.TryParse<MotelyPlayingCardRank>(Value, true, out var erraticRank))
                            {
                                RankEnum = erraticRank;
                            }
                            else
                            {
                                Console.WriteLine($"[InitParsedEnums] ErraticRank: FAILED to parse '{Value}' as MotelyPlayingCardRank! Valid values include: Ace, King, Queen, Jack, Ten, Nine, Eight, Seven, Six, Five, Four, Three, Two, or One.");
                            }
                            break;
                        case MotelyFilterItemType.ErraticSuit:
                            // Parse suit from Value
                            if (Enum.TryParse<MotelyPlayingCardSuit>(Value, true, out var erraticSuit))
                                SuitEnum = erraticSuit;
                            break;
                        case MotelyFilterItemType.SmallBlindTag:
                        case MotelyFilterItemType.BigBlindTag:
                            if (Enum.TryParse<MotelyTag>(Value, true, out var tag))
                                TagEnum = tag;
                            // Check if this was a generic "tag" type
                            if (Type?.ToLowerInvariant() == "tag")
                            {
                                TagTypeEnum = MotelyTagType.Any; // Generic tag matches both small and big blind
                            }
                            else
                            {
                                TagTypeEnum =
                                    ItemTypeEnum == MotelyFilterItemType.SmallBlindTag
                                        ? MotelyTagType.SmallBlind
                                        : MotelyTagType.BigBlind;
                            }
                            break;
                        case MotelyFilterItemType.PlayingCard:
                            // Parse "X of Y" format like "7 of Clubs"
                            if (Value.Contains(" of "))
                            {
                                var parts = Value.Split(
                                    " of ",
                                    StringSplitOptions.RemoveEmptyEntries
                                );
                                if (parts.Length == 2)
                                {
                                    var rankStr = parts[0].Trim();
                                    var suitStr = parts[1].Trim();

                                    // Parse rank
                                    var rankEnum = rankStr switch
                                    {
                                        "2" => MotelyPlayingCardRank.Two,
                                        "3" => MotelyPlayingCardRank.Three,
                                        "4" => MotelyPlayingCardRank.Four,
                                        "5" => MotelyPlayingCardRank.Five,
                                        "6" => MotelyPlayingCardRank.Six,
                                        "7" => MotelyPlayingCardRank.Seven,
                                        "8" => MotelyPlayingCardRank.Eight,
                                        "9" => MotelyPlayingCardRank.Nine,
                                        "10" => MotelyPlayingCardRank.Ten,
                                        "Jack" => MotelyPlayingCardRank.Jack,
                                        "Queen" => MotelyPlayingCardRank.Queen,
                                        "King" => MotelyPlayingCardRank.King,
                                        "Ace" => MotelyPlayingCardRank.Ace,
                                        _ => (MotelyPlayingCardRank?)null,
                                    };

                                    // Parse suit
                                    var suitEnum = suitStr switch
                                    {
                                        "Clubs" => MotelyPlayingCardSuit.Club,
                                        "Diamonds" => MotelyPlayingCardSuit.Diamond,
                                        "Hearts" => MotelyPlayingCardSuit.Heart,
                                        "Spades" => MotelyPlayingCardSuit.Spade,
                                        _ => (MotelyPlayingCardSuit?)null,
                                    };

                                    if (rankEnum.HasValue)
                                        RankEnum = rankEnum.Value;
                                    if (suitEnum.HasValue)
                                        SuitEnum = suitEnum.Value;
                                }
                            }
                            break;
                    }
                }
            }

            // Parse Edition
            if (!string.IsNullOrEmpty(Edition))
            {
                // Handle "NoEdition" alias for "None"
                var editionStr = Edition.Equals("NoEdition", StringComparison.OrdinalIgnoreCase)
                    ? "None"
                    : Edition;

                if (Enum.TryParse<MotelyItemEdition>(editionStr, true, out var edition))
                    EditionEnum = edition;
            }

            // Parse Stickers
            if (Stickers != null && Stickers.Count > 0)
            {
                StickerEnums = new List<MotelyJokerSticker>();
                foreach (var sticker in Stickers)
                {
                    if (Enum.TryParse<MotelyJokerSticker>(sticker, true, out var stickerEnum))
                        StickerEnums.Add(stickerEnum);
                }
            }

            // Parse Values array (multi-value support)
            if (Values != null && Values.Length > 0)
            {
                // Validate mutual exclusivity with Value
                if (!string.IsNullOrEmpty(Value))
                    throw new ArgumentException(
                        "Cannot specify both 'Value' and 'Values' properties. Use only one."
                    );

                // Parse multiple enum values based on type
                switch (ItemTypeEnum)
                {
                    case MotelyFilterItemType.Joker:
                        JokerEnums = new List<MotelyJoker>();
                        foreach (var value in Values)
                        {
                            if (Enum.TryParse<MotelyJoker>(value, true, out var joker))
                            {
                                // Helpful error for common mistake: using "Perkeo" with regular Joker type
                                if (joker == MotelyJoker.Perkeo)
                                {
                                    throw new ArgumentException(
                                        $"'{value}' is not a valid regular Joker. Did you mean to use 'SoulJoker' type instead of 'Joker'? Perkeo can only appear as a Soul Joker."
                                    );
                                }
                                JokerEnums.Add(joker);
                            }
                            else if (
                                string.Equals(value, "Perkeo", StringComparison.OrdinalIgnoreCase)
                            )
                            {
                                throw new ArgumentException(
                                    $"'{value}' is not a valid regular Joker. Did you mean to use 'SoulJoker' type instead of 'Joker'? Perkeo can only appear as a Soul Joker."
                                );
                            }
                        }
                        break;
                    case MotelyFilterItemType.SoulJoker:
                        JokerEnums = new List<MotelyJoker>();
                        foreach (var value in Values)
                        {
                            if (Enum.TryParse<MotelyJoker>(value, true, out var joker))
                                JokerEnums.Add(joker);
                        }
                        break;
                    case MotelyFilterItemType.Voucher:
                        VoucherEnums = new List<MotelyVoucher>();
                        foreach (var value in Values)
                        {
                            if (Enum.TryParse<MotelyVoucher>(value, true, out var voucher))
                                VoucherEnums.Add(voucher);
                        }
                        break;
                    case MotelyFilterItemType.TarotCard:
                        TarotEnums = new List<MotelyTarotCard>();
                        foreach (var value in Values)
                        {
                            if (Enum.TryParse<MotelyTarotCard>(value, true, out var tarot))
                                TarotEnums.Add(tarot);
                        }
                        break;
                    case MotelyFilterItemType.PlanetCard:
                        PlanetEnums = new List<MotelyPlanetCard>();
                        foreach (var value in Values)
                        {
                            if (Enum.TryParse<MotelyPlanetCard>(value, true, out var planet))
                                PlanetEnums.Add(planet);
                        }
                        break;
                    case MotelyFilterItemType.SpectralCard:
                        SpectralEnums = new List<MotelySpectralCard>();
                        foreach (var value in Values)
                        {
                            if (Enum.TryParse<MotelySpectralCard>(value, true, out var spectral))
                                SpectralEnums.Add(spectral);
                        }
                        break;
                    case MotelyFilterItemType.SmallBlindTag:
                    case MotelyFilterItemType.BigBlindTag:
                        TagEnums = new List<MotelyTag>();
                        foreach (var value in Values)
                        {
                            if (Enum.TryParse<MotelyTag>(value, true, out var tag))
                                TagEnums.Add(tag);
                        }
                        // Set TagTypeEnum based on type
                        if (Type?.ToLowerInvariant() == "tag")
                        {
                            TagTypeEnum = MotelyTagType.Any;
                        }
                        else
                        {
                            TagTypeEnum =
                                ItemTypeEnum == MotelyFilterItemType.SmallBlindTag
                                    ? MotelyTagType.SmallBlind
                                    : MotelyTagType.BigBlind;
                        }
                        break;
                    case MotelyFilterItemType.Boss:
                        BossEnums = new List<MotelyBossBlind>();
                        foreach (var value in Values)
                        {
                            if (Enum.TryParse<MotelyBossBlind>(value, true, out var boss))
                                BossEnums.Add(boss);
                        }
                        break;
                }
            }

            // Parse PlayingCard specific properties
            if (ItemTypeEnum == MotelyFilterItemType.PlayingCard)
            {
                // Parse Suit - treat "Any" or "*" as not specified
                if (
                    !string.IsNullOrEmpty(Suit)
                    && !Suit.Equals("Any", StringComparison.OrdinalIgnoreCase)
                    && !Suit.Equals("*", StringComparison.OrdinalIgnoreCase)
                    && Enum.TryParse<MotelyPlayingCardSuit>(Suit, true, out var suit)
                )
                {
                    SuitEnum = suit;
                }

                // Parse Rank - treat "Any" or "*" as not specified
                if (
                    !string.IsNullOrEmpty(Rank)
                    && !Rank.Equals("Any", StringComparison.OrdinalIgnoreCase)
                    && !Rank.Equals("*", StringComparison.OrdinalIgnoreCase)
                    && Enum.TryParse<MotelyPlayingCardRank>(Rank, true, out var rank)
                )
                {
                    RankEnum = rank;
                }

                if (
                    !string.IsNullOrEmpty(Seal)
                    && Enum.TryParse<MotelyItemSeal>(Seal, true, out var seal)
                )
                    SealEnum = seal;

                if (
                    !string.IsNullOrEmpty(Enhancement)
                    && Enum.TryParse<MotelyItemEnhancement>(Enhancement, true, out var enhancement)
                )
                    EnhancementEnum = enhancement;
            }

            // Parse EventType for Event filters
            if (ItemTypeEnum == MotelyFilterItemType.Event)
            {
                // For event clauses, the event type comes from the Value property
                if (!string.IsNullOrEmpty(Value))
                {
                    if (Enum.TryParse<MotelyEventType>(Value, true, out var eventType))
                    {
                        EventTypeEnum = eventType;
                    }
                    else
                        throw new ArgumentException($"'{Value}' is not a valid EventType value.");
                }
                else
                {
                    throw new ArgumentException("Event clause missing Value property - cannot determine event type");
                }
            }
        }

        /// <summary>
        /// Copy all parsed enum fields from another clause.
        /// Used when cloning clauses for ante-specific filtering.
        /// NOTE: This does NOT copy AntesWasExplicitlySet - that should be set explicitly by the caller
        /// based on whether the cloning operation is intentionally setting Antes.
        /// </summary>
        public void CopyParsedEnumsFrom(MotelyJsonFilterClause source)
        {
            this.ItemTypeEnum = source.ItemTypeEnum;
            this.VoucherEnum = source.VoucherEnum;
            this.TarotEnum = source.TarotEnum;
            this.PlanetEnum = source.PlanetEnum;
            this.SpectralEnum = source.SpectralEnum;
            this.JokerEnum = source.JokerEnum;
            this.TagEnum = source.TagEnum;
            this.TagTypeEnum = source.TagTypeEnum;
            this.BossEnum = source.BossEnum;
            this.JokerEnums = source.JokerEnums;
            this.VoucherEnums = source.VoucherEnums;
            this.TarotEnums = source.TarotEnums;
            this.PlanetEnums = source.PlanetEnums;
            this.SpectralEnums = source.SpectralEnums;
            this.TagEnums = source.TagEnums;
            this.BossEnums = source.BossEnums;
            this.EditionEnum = source.EditionEnum;
            this.StickerEnums = source.StickerEnums;
            this.SuitEnum = source.SuitEnum;
            this.RankEnum = source.RankEnum;
            this.SealEnum = source.SealEnum;
            this.EnhancementEnum = source.EnhancementEnum;
            this.WildcardEnum = source.WildcardEnum;
            this.IsWildcard = source.IsWildcard;
        }
    }

    // Pre-computed expensive calculations (set during PostProcess, immutable after)
    [JsonIgnore]
    [YamlIgnore]
    public int MaxVoucherAnte { get; private set; }

    [JsonIgnore]
    [YamlIgnore]
    public int MaxBossAnte { get; private set; }

    // SourcesConfig moved to SourcesConfig.cs

    /// <summary>
    /// Try to load configuration from JSON file
    /// </summary>
    /// <param name="jsonPath">Path to the JSON configuration file</param>
    /// <param name="config">The loaded configuration if successful</param>
    /// <returns>True if loading and validation succeeded, false otherwise</returns>
    public static bool TryLoadFromJsonFile(
        string jsonPath,
        [NotNullWhen(true)] out MotelyJsonConfig? config
    )
    {
        return TryLoadFromJsonFile(jsonPath, out config, out _);
    }

    public static bool TryLoadFromJsonFile(
        string jsonPath,
        [NotNullWhen(true)] out MotelyJsonConfig? config,
        out string? error
    )
    {
        config = null;
        error = null;

        if (!File.Exists(jsonPath))
        {
            error = $"File not found: {jsonPath}";
            return false;
        }

        try
        {
            var json = File.ReadAllText(jsonPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, // Reject typos like "Valuie:" instead of "value"
            };

            var deserializedConfig = JsonSerializer.Deserialize<MotelyJsonConfig>(json, options);
            if (deserializedConfig == null)
            {
                error = "Failed to deserialize JSON - result was null";
                return false;
            }

            deserializedConfig.PostProcess();

            // Validate config
            MotelyJsonConfigValidator.ValidateConfig(deserializedConfig);

            config = deserializedConfig;
            return true;
        }
        catch (JsonException jex)
        {
            // Get the line and position info for JSON errors
            var baseError =
                $"JSON syntax error at line {jex.LineNumber}, position {jex.BytePositionInLine}: {jex.Message}";

            // Provide helpful hints for common errors
            if (
                jex.Message.Contains(
                    "System.Nullable`1[System.Boolean]",
                    StringComparison.OrdinalIgnoreCase
                )
                || (
                    jex.Message.Contains("System.Boolean", StringComparison.OrdinalIgnoreCase)
                    && jex.Message.Contains(
                        "could not be converted",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                error =
                    $"{baseError}\n💡 Hint: Booleans use true or false without quotes. Change \"true\" to true and \"false\" to false.\n   Example: \"requireMega\": true (correct) vs \"requireMega\": \"true\" (wrong)";
            }
            else if (
                jex.Message.Contains("System.String[]", StringComparison.OrdinalIgnoreCase)
                || (
                    jex.Message.Contains("values", StringComparison.OrdinalIgnoreCase)
                    && jex.Message.Contains(
                        "could not be converted",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                error =
                    $"{baseError}\n💡 Hint: 'values' expects an array. Did you mean to use 'value' (single string) instead of 'values' (array)?\n   Example: \"value\": \"TheMagician\" or \"values\": [\"TheMagician\", \"TheHierophant\"]";
            }
            else if (
                jex.Message.Contains("cannot be mapped to")
                || jex.Message.Contains("Could not convert")
            )
            {
                error =
                    $"{baseError}\n💡 Hint: Check that array properties use [] brackets and single values don't.";
            }
            else
            {
                error = baseError;
            }

            DebugLogger.Log($"Config loading failed for {jsonPath}: {error}");
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            DebugLogger.Log($"Config loading failed for {jsonPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Recursively process a single clause and all its nested clauses
    /// </summary>
    private void ProcessClause(MotelyJsonFilterClause item)
    {
        DebugLogger.Log(
            $"[PROCESS START] Type={item.Type}, Value={item.Value}, Antes={(item.Antes == null ? "null" : $"[{string.Join(",", item.Antes)}]")}, MinShop={item.MinShopSlot}, MaxShop={item.MaxShopSlot}"
        );
        // Normalize type
        item.Type = item.Type.ToLowerInvariant();

        // Normalize arrays - but DON'T initialize flat pack/shop slots as that breaks Sources merging
        // item.PackSlots and item.ShopSlots should remain null if not provided
        item.Stickers ??= [];

        // Track whether Antes was explicitly set (before we default it)
        // This is CRITICAL for OR/AND helper behavior - we need to distinguish:
        // 1. User explicitly set Antes=[1,2] -> use helper behavior (propagate to children)
        // 2. User didn't set Antes (null/empty) -> defaulted to all antes, DON'T propagate
        bool antesWasExplicitlySet = item.Antes != null && item.Antes.Length > 0;
        item.AntesWasExplicitlySet = antesWasExplicitlySet;

        // Default to all antes if null OR empty (explicit empty array should also get default)
        // Or/And clauses CAN have their own antes that restrict when the entire clause is evaluated!
        if (item.Antes == null || item.Antes.Length == 0)
        {
            // Use user-configured defaults if available, otherwise fallback to hardcoded defaults
            var defaults = Defaults ?? new MotelyFilterDefaults();
            item.Antes = defaults.GetEffectiveAntes();
        }

        // Don't initialize empty arrays - let min/max populate them later
        // if (item.Sources != null)
        // {
        //     item.Sources.PackSlots ??= [];
        //     item.Sources.ShopSlots ??= [];
        // }

        // Parse all enums ONCE to avoid string operations in hot path
        item.InitializeParsedEnums();

        // CRITICAL: Preserve Sources if it was explicitly specified in JAML (deserialized by YamlDotNet)
        // Even if all properties are empty/null, if sources: was specified, Sources should exist.
        // If Sources exists at this point, it means sources: was explicitly specified in JAML.
        // Store this BEFORE any code that might modify Sources
        bool sourcesWasExplicitlySpecified = item.Sources != null;

        // Merge flat properties into Sources for backwards compatibility
        DebugLogger.Log(
            $"[MERGE] Type={item.Type}, Value={item.Value}, flat ShopSlots={(item.ShopSlots == null ? "null" : $"[{string.Join(",", item.ShopSlots)}]")}, MinShop={item.MinShopSlot}, MaxShop={item.MaxShopSlot}"
        );
        if (
            item.PackSlots != null
            || item.ShopSlots != null
            || item.RequireMega != null
            || item.Tags != null
        )
        {
            if (item.Sources == null)
            {
                item.Sources = new SourcesConfig();
            }

            if (item.PackSlots != null)
            {
                item.Sources.PackSlots = item.PackSlots;
            }
            if (item.ShopSlots != null)
            {
                DebugLogger.Log(
                    $"[MERGE] Copying flat ShopSlots [{string.Join(",", item.ShopSlots)}] to Sources.ShopSlots"
                );
                item.Sources.ShopSlots = item.ShopSlots;
            }
            if (item.RequireMega != null)
                item.Sources.RequireMega = item.RequireMega.Value;
            if (item.Tags != null)
                item.Sources.Tags = item.Tags.Value;
        }

        // Don't apply GetDefaultSources anymore - we use ante-based defaults dynamically!
        // Only apply defaults for special cases that REQUIRE specific sources (like soul jokers pack-only)
        // CRITICAL: If Sources was explicitly specified in JAML (sourcesWasExplicitlySpecified = true),
        // we must NOT overwrite it with defaults. Sources should be preserved even if all properties are empty.
        if (
            item.Sources == null
            && !sourcesWasExplicitlySpecified
            && item.ItemTypeEnum != MotelyFilterItemType.And
            && item.ItemTypeEnum != MotelyFilterItemType.Or
        )
        {
            if (item.Type == "souljoker")
            {
                // Soul jokers ONLY appear in packs, never shops
                item.Sources = new SourcesConfig
                {
                    ShopSlots = Array.Empty<int>(),
                    PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                    Tags = true,
                };
            }
            else if (item.Type == "spectralcard")
            {
                // Spectral cards have deck-specific defaults
                item.Sources = GetSpectralCardDefaultSources(item.Value, Deck ?? "Red");
            }
            else if (item.Type is "tag" or "smallblindtag" or "bigblindtag")
            {
                // Tags don't appear in slots
                item.Sources = new SourcesConfig
                {
                    ShopSlots = Array.Empty<int>(),
                    PackSlots = Array.Empty<int>(),
                    Tags = true,
                };
            }
            else if (item.Type is "standardcard" or "playingcard")
            {
                // Playing cards appear in packs (shop not supported yet)
                item.Sources = new SourcesConfig
                {
                    ShopSlots = Array.Empty<int>(),
                    PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                    Tags = false,
                };
            }
            else
            {
                // Apply user-configured defaults (if specified) for items that don't have explicit Sources
                // This allows users to set default pack/shop slots in their JAML config
                if (Defaults != null && (Defaults.PackSlots != null || Defaults.ShopSlots != null))
                {
                    // Note: We can't apply ante-specific defaults here because we don't know which ante yet
                    // The clause might check multiple antes. Defaults will be applied per-ante during filtering.
                    // For now, just mark that we have user defaults available.
                    // Actual per-ante slot filtering happens in the hot path.
                }
                // ELSE: Leave Sources as null so ante-based defaults apply during filtering!
            }
        }

        // RECURSIVELY process nested clauses for And/Or
        if (item.Clauses != null && item.Clauses.Count > 0)
        {
            foreach (var nestedClause in item.Clauses)
            {
                ProcessClause(nestedClause);
            }
        }

        // Populate Sources.ShopSlots/PackSlots from min/max if needed
        // CRITICAL FIX: When sources: is specified in JAML (even with empty arrays),
        // Sources should exist. The problem is that YamlDotNet might not create SourcesConfig
        // when all properties are null/empty, even if sources: was specified.
        // 
        // SOLUTION: If Sources was explicitly specified (sourcesWasExplicitlySpecified = true),
        // ensure it exists. Then populate from min/max if needed.
        if (sourcesWasExplicitlySpecified && item.Sources == null)
        {
            // Sources was detected earlier but is now null - recreate it
            // This handles the case where YamlDotNet didn't create SourcesConfig
            // even though sources: was specified in JAML
            item.Sources = new SourcesConfig();
        }
        
        if (item.Sources != null)
        {
            // Sources exists (either was deserialized or we just created it) - populate from min/max
            if (item.Sources.MinShopSlot.HasValue || item.Sources.MaxShopSlot.HasValue)
            {
                int minSlot = item.Sources.MinShopSlot ?? 0;
                int maxSlot = item.Sources.MaxShopSlot ?? MotelySlotLimits.MAX_SHOP_SLOT;
                int count = maxSlot - minSlot + 1;
                if (count > 0 && maxSlot <= MotelySlotLimits.MAX_SHOP_SLOT)
                {
                    int[] shopSlots = new int[Math.Min(count, MotelySlotLimits.MAX_SHOP_SLOT - minSlot + 1)];
                    int index = 0;
                    for (int i = minSlot; i <= maxSlot && i <= MotelySlotLimits.MAX_SHOP_SLOT; i++)
                        shopSlots[index++] = i;
                    item.Sources.ShopSlots = shopSlots;
                }
                else
                {
                    item.Sources.ShopSlots = [];
                }
                item.MinShopSlot = minSlot;
                item.MaxShopSlot = maxSlot;
            }
            else if (item.Sources.ShopSlots != null && item.Sources.ShopSlots.Length > 0)
            {
                item.MinShopSlot = item.Sources.ShopSlots.Min();
                item.MaxShopSlot = item.Sources.ShopSlots.Max();
            }

            // Same logic for packSlots - don't auto-populate if explicitly empty
            bool packSlotsExplicitlyEmpty = item.Sources.PackSlots != null && item.Sources.PackSlots.Length == 0;
            
            if ((item.Sources.MinPackSlot.HasValue || item.Sources.MaxPackSlot.HasValue) && !packSlotsExplicitlyEmpty)
            {
                // Only populate if packSlots wasn't explicitly set to empty array
                int minSlot = item.Sources.MinPackSlot ?? 0;
                int maxSlot = item.Sources.MaxPackSlot ?? MotelySlotLimits.MAX_PACK_SLOT;
                int count = maxSlot - minSlot + 1;
                if (count > 0 && maxSlot <= MotelySlotLimits.MAX_PACK_SLOT)
                {
                    int[] packSlots = new int[Math.Min(count, MotelySlotLimits.MAX_PACK_SLOT - minSlot + 1)];
                    int index = 0;
                    for (int i = minSlot; i <= maxSlot && i <= MotelySlotLimits.MAX_PACK_SLOT; i++)
                        packSlots[index++] = i;
                    item.Sources.PackSlots = packSlots;
                }
                else
                {
                    item.Sources.PackSlots = [];
                }
                item.MinPackSlot = minSlot;
                item.MaxPackSlot = maxSlot;
            }
            else if (item.Sources.PackSlots != null && item.Sources.PackSlots.Length > 0)
            {
                // packSlots was explicitly set (non-empty) - derive min/max from it
                item.MinPackSlot = item.Sources.PackSlots.Min();
                item.MaxPackSlot = item.Sources.PackSlots.Max();
            }
            else if (item.Sources.MinPackSlot.HasValue || item.Sources.MaxPackSlot.HasValue)
            {
                // min/max are set but packSlots is explicitly empty - just set min/max, don't populate packSlots
                item.MinPackSlot = item.Sources.MinPackSlot ?? 0;
                item.MaxPackSlot = item.Sources.MaxPackSlot ?? MotelySlotLimits.MAX_PACK_SLOT;
            }
        }
    }

    /// <summary>
    /// Post-process after deserialization
    /// </summary>
    public void PostProcess()
    {
        DebugLogger.Log($"[PostProcess] START");

        // Parse top-level mode (score aggregation)
        if (!string.IsNullOrWhiteSpace(Mode))
        {
            var m = Mode.Trim();
            if (m.Equals("sum", StringComparison.OrdinalIgnoreCase))
            {
                ScoreAggregationMode = MotelyScoreAggregationMode.Sum;
            }
            else if (
                m.Equals("max", StringComparison.OrdinalIgnoreCase)
                || m.Equals("max_count", StringComparison.OrdinalIgnoreCase)
                || m.Equals("maxcount", StringComparison.OrdinalIgnoreCase)
            )
            {
                ScoreAggregationMode = MotelyScoreAggregationMode.MaxCount;
            }
            else
            {
                // Unknown values will be handled by validator; default to Sum
                ScoreAggregationMode = MotelyScoreAggregationMode.Sum;
            }
        }

        DebugLogger.Log($"[PostProcess] About to process clauses");

        // Process all filter items recursively (handles nested And/Or clauses)
        var sections = new[] { ("must", Must ?? []), ("should", Should ?? []), ("mustNot", MustNot ?? []) };
        foreach (var (sectionName, items) in sections)
        {
            DebugLogger.Log($"[PostProcess] Processing section: {sectionName}, count={items.Count}");
            for (int i = 0; i < items.Count; i++)
            {
                try
                {
                    ProcessClause(items[i]);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Error in {sectionName}[{i}]: {ex.Message}", ex);
                }
            }
        }

        DebugLogger.Log($"[PostProcess] Finished processing clauses. Starting voucher partitioning. Must.Count={Must?.Count}, Should.Count={Should?.Count}");

        // PERFORMANCE: Pre-partition clauses by type to avoid repeated iteration in hot paths
        // Count first to avoid List reallocation
        int mustVoucherCount = 0, mustNonVoucherCount = 0;
        int shouldVoucherCount = 0, shouldNonVoucherCount = 0;

        foreach (var clause in Must ?? [])
        {
            if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
                mustVoucherCount++;
            else
                mustNonVoucherCount++;
        }

        foreach (var clause in Should ?? [])
        {
            if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
                shouldVoucherCount++;
            else
                shouldNonVoucherCount++;
        }

        // Allocate exact-size arrays (zero-allocation partitioning)
        var mustVouchers = new MotelyJsonFilterClause[mustVoucherCount];
        var mustNonVouchers = new MotelyJsonFilterClause[mustNonVoucherCount];
        var shouldVouchers = new MotelyJsonFilterClause[shouldVoucherCount];
        var shouldNonVouchers = new MotelyJsonFilterClause[shouldNonVoucherCount];

        int mvIdx = 0, mnvIdx = 0, svIdx = 0, snvIdx = 0;

        foreach (var clause in Must ?? [])
        {
            if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
                mustVouchers[mvIdx++] = clause;
            else
                mustNonVouchers[mnvIdx++] = clause;
        }

        foreach (var clause in Should ?? [])
        {
            if (clause.ItemTypeEnum == MotelyFilterItemType.Voucher)
                shouldVouchers[svIdx++] = clause;
            else
                shouldNonVouchers[snvIdx++] = clause;
        }

        MustVouchers = mustVouchers;
        MustNonVouchers = mustNonVouchers;
        ShouldVouchers = shouldVouchers;
        ShouldNonVouchers = shouldNonVouchers;

        DebugLogger.Log($"[PostProcess] Partitioned. MustVouchers={MustVouchers.Length}, ShouldVouchers={ShouldVouchers.Length}");

        // Compute MaxVoucherAnte once during PostProcess (use pre-partitioned arrays!)
        int maxAnte = 0;
        foreach (var clause in MustVouchers)
        {
            maxAnte = Math.Max(
                maxAnte,
                clause.EffectiveAntes.Length > 0 ? clause.EffectiveAntes.Max() : 1
            );
        }
        foreach (var clause in ShouldVouchers)
        {
            maxAnte = Math.Max(
                maxAnte,
                clause.EffectiveAntes.Length > 0 ? clause.EffectiveAntes.Max() : 1
            );
        }
        MaxVoucherAnte = maxAnte;
#if DEBUG
        DebugLogger.Log($"[Config] MaxVoucherAnte calculated as: {MaxVoucherAnte}");
#endif

        // Compute MaxBossAnte once during PostProcess (check BOTH Must and Should)
        int maxBossAnte = 0;
        foreach (var clause in Must ?? [])
        {
            if (clause.ItemTypeEnum == MotelyFilterItemType.Boss)
                maxBossAnte = Math.Max(maxBossAnte,
                    clause.EffectiveAntes.Length > 0 ? clause.EffectiveAntes.Max() : 1);
        }
        foreach (var clause in Should ?? [])
        {
            if (clause.ItemTypeEnum == MotelyFilterItemType.Boss)
                maxBossAnte = Math.Max(maxBossAnte,
                    clause.EffectiveAntes.Length > 0 ? clause.EffectiveAntes.Max() : 1);
        }
        MaxBossAnte = maxBossAnte;
#if DEBUG
        DebugLogger.Log($"[Config] MaxBossAnte calculated as: {MaxBossAnte}");
#endif

        // CRITICAL VALIDATION: Ensure all MUST clauses are properly parsed
        foreach (var clause in Must ?? [])
        {
            // For Event clauses, ensure EventTypeEnum is set
            if (clause.ItemTypeEnum == MotelyFilterItemType.Event && !clause.EventTypeEnum.HasValue)
            {
                throw new ArgumentException($"CRITICAL: MUST Event clause failed to parse - missing EventTypeEnum. Type={clause.Type}, Value={clause.Value}");
            }
            
            // For And/Or clauses, ensure they have nested clauses
            if ((clause.ItemTypeEnum == MotelyFilterItemType.And || clause.ItemTypeEnum == MotelyFilterItemType.Or) 
                && (clause.Clauses == null || clause.Clauses.Count == 0))
            {
                throw new ArgumentException($"CRITICAL: MUST {clause.ItemTypeEnum} clause has no nested clauses");
            }
        }
    }

    private static SourcesConfig GetDefaultSources(string itemType, string? itemValue, string deck)
    {
        return itemType switch
        {
            "souljoker" => new SourcesConfig
            {
                ShopSlots = Array.Empty<int>(), // Legendary jokers can't appear in shops
                PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                Tags = true,
            },
            "spectralcard" => GetSpectralCardDefaultSources(itemValue, deck),
            "tag" or "smallblindtag" or "bigblindtag" => new SourcesConfig
            {
                ShopSlots = Array.Empty<int>(), // Tags don't appear in shop slots
                PackSlots = Array.Empty<int>(), // Tags don't appear in pack slots
                Tags = true,
            },
            _ => new SourcesConfig
            {
                ShopSlots = new[] { 0, 1, 2, 3 },
                PackSlots = new[] { 0, 1, 2, 3 },
                Tags = true,
            },
        };
    }

    private static SourcesConfig GetSpectralCardDefaultSources(
        string? spectralCardValue,
        string deck
    )
    {
        // BlackHole and Soul spectral cards never appear in shop slots
        bool isBlackHoleOrSoul =
            !string.IsNullOrEmpty(spectralCardValue)
            && (
                string.Equals(spectralCardValue, "BlackHole", StringComparison.OrdinalIgnoreCase)
                || string.Equals(spectralCardValue, "Soul", StringComparison.OrdinalIgnoreCase)
            );

        // Other spectral cards only appear in shop slots with Ghost Deck
        bool isGhostDeck = string.Equals(deck, "Ghost", StringComparison.OrdinalIgnoreCase);

        return new SourcesConfig
        {
            ShopSlots = isBlackHoleOrSoul
                ? Array.Empty<int>()
                : (isGhostDeck ? new[] { 0, 1, 2, 3 } : Array.Empty<int>()),
            PackSlots = new[] { 0, 1, 2, 3 },
            Tags = true,
        };
    }

    /// <summary>
    /// Convert to JSON string
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Get column names for DuckDB/CSV output
    /// Returns: ["seed", "score", "column1", "column2", ...]
    /// SHARED between DuckDB schema creation and CSV export!
    /// </summary>
    public List<string> GetColumnNames()
    {
        var columns = new List<string> { "seed", "score" };
        var usedNames = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);

        foreach (var clause in Should ?? [])
        {
            var columnName = GetClauseColumnName(clause);

            // Ensure unique column names by adding suffix if duplicate
            var uniqueName = columnName;
            int suffix = 2;
            while (usedNames.Contains(uniqueName))
            {
                uniqueName = $"{columnName}_{suffix}";
                suffix++;
            }

            usedNames.Add(uniqueName);
            columns.Add(uniqueName);
        }

        return columns;
    }

    /// <summary>
    /// Generate a human-readable column name for a filter clause
    /// Supports spaces and proper casing - will be quoted in CSV/DuckDB
    /// </summary>
    private static string GetClauseColumnName(MotelyJsonFilterClause clause)
    {
        // Use label if provided (highest priority - keep original formatting!)
        if (!string.IsNullOrEmpty(clause.Label))
            return clause.Label;

        // Handle OR/AND clauses with compact notation
        if ((clause.Type?.ToLower() == "or" || clause.Type?.ToLower() == "and") && clause.Clauses != null && clause.Clauses.Count > 0)
        {
            var clauseType = clause.Type.ToUpper();
            var count = clause.Clauses.Count;
            var anteSuffix = "";
            if (clause.Antes != null && clause.Antes.Length > 0 && clause.Antes.Length < 8)
            {
                // Human-readable ante range: A1-3 instead of A1A2A3
                var minAnte = clause.Antes.Min();
                var maxAnte = clause.Antes.Max();
                anteSuffix = minAnte == maxAnte ? $" A{minAnte}" : $" A{minAnte}-{maxAnte}";
            }

            return $"{count} {clauseType}{anteSuffix}";
        }

        // Build name from value/type
        string name;
        if (!string.IsNullOrEmpty(clause.Value))
        {
            // Special handling for wildcards (Any)
            if (clause.Value.Equals("Any", StringComparison.OrdinalIgnoreCase))
            {
                name = $"Any_{clause.Type}";
            }
            else
            {
                name = clause.Value;
            }
        }
        else if (clause.Values != null && clause.Values.Length > 0)
        {
            // Multi-value case: Use first value + count indicator
            if (clause.Values.Length == 1)
            {
                name = clause.Values[0];
            }
            else
            {
                // Multiple values: create descriptive name
                name = $"{clause.Values[0]}_Plus{clause.Values.Length - 1}More";
            }
        }
        else
        {
            // Fallback to type
            name = clause.Type ?? "Unknown";
        }

        // Add edition prefix if specified
        if (!string.IsNullOrEmpty(clause.Edition))
            name = clause.Edition + " " + name; // Space instead of underscore!

        // Add ante suffix if specified (human-readable range format)
        if (clause.Antes != null && clause.Antes.Length > 0 && clause.Antes.Length < 8)
        {
            var minAnte = clause.Antes.Min();
            var maxAnte = clause.Antes.Max();
            name += minAnte == maxAnte ? $" A{minAnte}" : $" A{minAnte}-{maxAnte}";
        }

        return name; // NO MakeSafeColumnName - keep it beautiful!
    }

    /// <summary>
    /// Convert any string to a safe SQL column name (lowercase, alphanumeric + underscore only)
    /// Max 63 chars for PostgreSQL/DuckDB compatibility
    /// </summary>
    private static string MakeSafeColumnName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "column";

        var safeName = System
            .Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "_")
            .ToLower();

        // SQL columns can't start with a digit
        if (char.IsDigit(safeName[0]))
            safeName = "col_" + safeName;

        // Limit to 63 characters (PostgreSQL/DuckDB limit)
        if (safeName.Length > 63)
            safeName = safeName.Substring(0, 63);

        return safeName;
    }

    /// <summary>
    /// Parse shorthand syntax like `event: luckyMoney` into filter clause
    /// </summary>
    public static MotelyJsonFilterClause ParseShorthand(string shorthand)
    {
        Console.WriteLine($"Parsing shorthand: {shorthand}"); // Debugging log

        if (shorthand.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            var clause = new MotelyJsonFilterClause
            {
                Type = "LuckyMoney",
                Value = shorthand.Substring("event:".Length).Trim()
            };

            Console.WriteLine($"Parsed clause: Type={clause.Type}, Value={clause.Value}"); // Debugging log
            return clause;
        }

        throw new ArgumentException("Invalid shorthand syntax", nameof(shorthand));
    }

    // Create a utility class for centralized parsing logic
    public static class MotelyEnumParser
    {
        private static readonly Dictionary<string, MotelyJoker> JokerLookup = Enum.GetValues(typeof(MotelyJoker))
            .Cast<MotelyJoker>()
            .ToDictionary(j => j.ToString(), j => j, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, MotelyVoucher> VoucherLookup = Enum.GetValues(typeof(MotelyVoucher))
            .Cast<MotelyVoucher>()
            .ToDictionary(v => v.ToString(), v => v, StringComparer.OrdinalIgnoreCase);

        public static bool TryParseJoker(string value, out MotelyJoker joker)
        {
            return JokerLookup.TryGetValue(value, out joker);
        }

        public static bool TryParseVoucher(string value, out MotelyVoucher voucher)
        {
            return VoucherLookup.TryGetValue(value, out voucher);
        }

        // Add similar methods for other enums as needed
    }
}
