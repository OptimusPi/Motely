using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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
        AnyPlanet
    }

    /// <summary>
    /// MongoDB compound Operator-style JSON configuration
    /// </summary>
    public class MotelyJsonConfig
    {
        // Metadata fields
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("author")]
        public string? Author { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("dateCreated")]
        public DateTime? DateCreated { get; set; }
        [JsonPropertyName("deck")]
        public string? Deck { get; set; } = "Red";
        [JsonPropertyName("stake")]
        public string? Stake { get; set; } = "White";
    
        [JsonPropertyName("must")]
        public List<MotleyJsonFilterClause> Must { get; set; } = new();
        [JsonPropertyName("should")]
        public List<MotleyJsonFilterClause> Should { get; set; } = new();
        [JsonPropertyName("mustNot")]
        public List<MotleyJsonFilterClause> MustNot { get; set; } = new();
        
    public class MotleyJsonFilterClause
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("value")]
        public string? Value { get; set; }
        [JsonPropertyName("values")]
        public string[]? Values { get; set; }
        [JsonPropertyName("label")]
        public string? Label { get; set; }
        [JsonPropertyName("antes")]
        public int[]? Antes { get; set; }

        // Nested clauses for And/Or grouping
        [JsonPropertyName("clauses")]
        public List<MotleyJsonFilterClause>? Clauses { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; } = 1;
        [JsonPropertyName("min")]
        public int? Min { get; set; }
        [JsonPropertyName("filterOrder")]
        public int? FilterOrder { get; set; }  // Optional ordering for slice chain optimization
        [JsonPropertyName("edition")]
        public string? Edition { get; set; }
        [JsonPropertyName("stickers")]
        public List<string>? Stickers { get; set; }
        
        // PlayingCard specific
        [JsonPropertyName("suit")]
        public string? Suit { get; set; }
        [JsonPropertyName("rank")]
        public string? Rank { get; set; }
        [JsonPropertyName("seal")]
        public string? Seal { get; set; }
        [JsonPropertyName("enhancement")]
        public string? Enhancement { get; set; }
        
        // Sources configuration
        [JsonPropertyName("sources")]
        public SourcesConfig? Sources { get; set; }
        
        // Direct properties for backwards compatibility  
        [JsonPropertyName("packSlots")]
        public int[]? PackSlots { get; set; }
        
        [JsonPropertyName("shopSlots")]
        public int[]? ShopSlots { get; set; }
        
        [JsonPropertyName("requireMega")]
        public bool? RequireMega { get; set; }
        
        [JsonPropertyName("tags")]
        public bool? Tags { get; set; }
        
        // Get effective antes
        [JsonIgnore]
        public int[] EffectiveAntes
        {
            get => Antes ?? [];
            set
            {
                Antes = value;
            }
        }

        // Pre-computed values (set during ProcessClause from Sources)
        // Min/Max are calculated from Sources.min/maxShopSlot or Sources.shopSlots array
        [JsonIgnore]
        public int? MinShopSlot { get; set; }
        [JsonIgnore]
        public int? MaxShopSlot { get; set; }
        [JsonIgnore]
        public int? MinPackSlot { get; set; }
        [JsonIgnore]
        public int? MaxPackSlot { get; set; }
        
        
        // Pre-parsed enum (set during initialization, immutable after)
        [JsonIgnore] 
        public MotelyFilterItemType ItemTypeEnum { get; private set; }
        
        [JsonIgnore] public MotelyVoucher? VoucherEnum { get; set; }
        [JsonIgnore] public MotelyTarotCard? TarotEnum { get; set; }
        [JsonIgnore] public MotelyPlanetCard? PlanetEnum { get; set; }
        [JsonIgnore] public MotelySpectralCard? SpectralEnum { get; set; }
        [JsonIgnore] public MotelyJoker? JokerEnum { get; set; }
        [JsonIgnore] public MotelyTag? TagEnum { get; set; }
        [JsonIgnore] public MotelyTagType TagTypeEnum { get; set; }
        [JsonIgnore] public MotelyBossBlind? BossEnum { get; set; }
        
        // Multi-value enum arrays for "values" property
        [JsonIgnore] public List<MotelyJoker>? JokerEnums { get; set; }
        [JsonIgnore] public List<MotelyVoucher>? VoucherEnums { get; set; }
        [JsonIgnore] public List<MotelyTarotCard>? TarotEnums { get; set; }
        [JsonIgnore] public List<MotelyPlanetCard>? PlanetEnums { get; set; }
        [JsonIgnore] public List<MotelySpectralCard>? SpectralEnums { get; set; }
        [JsonIgnore] public List<MotelyTag>? TagEnums { get; set; }
        [JsonIgnore] public List<MotelyBossBlind>? BossEnums { get; set; }
        [JsonIgnore] public MotelyItemEdition? EditionEnum { get; set; }
        [JsonIgnore] public List<MotelyJokerSticker>? StickerEnums { get; set; }
        [JsonIgnore] public MotelyPlayingCardSuit? SuitEnum { get; set; }
        [JsonIgnore] public MotelyPlayingCardRank? RankEnum { get; set; }
        [JsonIgnore] public MotelyItemSeal? SealEnum { get; set; }
        [JsonIgnore] public MotelyItemEnhancement? EnhancementEnum { get; set; }
        [JsonIgnore] public MotelyJsonConfigWildcards? WildcardEnum { get; set; }
        [JsonIgnore] public bool IsWildcard { get; set; }
        
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
                            if (Enum.TryParse<MotelyJoker>(Value, true, out var regularJoker))
                            {
                                JokerEnum = regularJoker;
                                
                                // Helpful error for common mistake: using "Perkeo" with regular Joker type
                                if (regularJoker == MotelyJoker.Perkeo)
                                {
                                    throw new ArgumentException($"'{Value}' is not a valid regular Joker. Did you mean to use 'SoulJoker' type instead of 'Joker'? Perkeo can only appear as a Soul Joker.");
                                }
                            }
                            else if (string.Equals(Value, "Perkeo", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new ArgumentException($"'{Value}' is not a valid regular Joker. Did you mean to use 'SoulJoker' type instead of 'Joker'? Perkeo can only appear as a Soul Joker.");
                            }
                            break;
                        case MotelyFilterItemType.SoulJoker:
                            if (Enum.TryParse<MotelyJoker>(Value, true, out var soulJoker))
                                JokerEnum = soulJoker;
                            break;
                        case MotelyFilterItemType.Voucher:
                            if (Enum.TryParse<MotelyVoucher>(Value, true, out var voucher))
                                VoucherEnum = voucher;
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
                                TagTypeEnum = ItemTypeEnum == MotelyFilterItemType.SmallBlindTag 
                                    ? MotelyTagType.SmallBlind 
                                    : MotelyTagType.BigBlind;
                            }
                            break;
                        case MotelyFilterItemType.PlayingCard:
                            // Parse "X of Y" format like "7 of Clubs"
                            if (Value.Contains(" of "))
                            {
                                var parts = Value.Split(" of ", StringSplitOptions.RemoveEmptyEntries);
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
                                        _ => (MotelyPlayingCardRank?)null
                                    };
                                    
                                    // Parse suit
                                    var suitEnum = suitStr switch
                                    {
                                        "Clubs" => MotelyPlayingCardSuit.Club,
                                        "Diamonds" => MotelyPlayingCardSuit.Diamond,
                                        "Hearts" => MotelyPlayingCardSuit.Heart,
                                        "Spades" => MotelyPlayingCardSuit.Spade,
                                        _ => (MotelyPlayingCardSuit?)null
                                    };
                                    
                                    if (rankEnum.HasValue) RankEnum = rankEnum.Value;
                                    if (suitEnum.HasValue) SuitEnum = suitEnum.Value;
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
                    throw new ArgumentException("Cannot specify both 'Value' and 'Values' properties. Use only one.");
                
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
                                    throw new ArgumentException($"'{value}' is not a valid regular Joker. Did you mean to use 'SoulJoker' type instead of 'Joker'? Perkeo can only appear as a Soul Joker.");
                                }
                                JokerEnums.Add(joker);
                            }
                            else if (string.Equals(value, "Perkeo", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new ArgumentException($"'{value}' is not a valid regular Joker. Did you mean to use 'SoulJoker' type instead of 'Joker'? Perkeo can only appear as a Soul Joker.");
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
                            TagTypeEnum = ItemTypeEnum == MotelyFilterItemType.SmallBlindTag 
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
                if (!string.IsNullOrEmpty(Suit) && 
                    !Suit.Equals("Any", StringComparison.OrdinalIgnoreCase) &&
                    !Suit.Equals("*", StringComparison.OrdinalIgnoreCase) &&
                    Enum.TryParse<MotelyPlayingCardSuit>(Suit, true, out var suit))
                {
                    SuitEnum = suit;
                }
                
                // Parse Rank - treat "Any" or "*" as not specified
                if (!string.IsNullOrEmpty(Rank) && 
                    !Rank.Equals("Any", StringComparison.OrdinalIgnoreCase) &&
                    !Rank.Equals("*", StringComparison.OrdinalIgnoreCase) &&
                    Enum.TryParse<MotelyPlayingCardRank>(Rank, true, out var rank))
                {
                    RankEnum = rank;
                }
                
                if (!string.IsNullOrEmpty(Seal) && Enum.TryParse<MotelyItemSeal>(Seal, true, out var seal))
                    SealEnum = seal;
                
                if (!string.IsNullOrEmpty(Enhancement) && Enum.TryParse<MotelyItemEnhancement>(Enhancement, true, out var enhancement))
                    EnhancementEnum = enhancement;
            }
        }
    }
    
        // Pre-computed expensive calculations (set during PostProcess, immutable after)
        [JsonIgnore]
        public int MaxVoucherAnte { get; private set; }
        
        [JsonIgnore]
        public int MaxBossAnte { get; private set; }

        public class SourcesConfig
        {
            [JsonPropertyName("shopSlots")]
            public int[]? ShopSlots { get; set; }

            [JsonPropertyName("packSlots")]
            public int[]? PackSlots { get; set; }

            [JsonPropertyName("minShopSlot")]
            public int? MinShopSlot { get; set; }

            [JsonPropertyName("maxShopSlot")]
            public int? MaxShopSlot { get; set; }

            [JsonPropertyName("minPackSlot")]
            public int? MinPackSlot { get; set; }

            [JsonPropertyName("maxPackSlot")]
            public int? MaxPackSlot { get; set; }
        
            [JsonPropertyName("tags")]
            public bool? Tags { get; set; }
        
            [JsonPropertyName("requireMega")]
            public bool? RequireMega { get; set; }
        }
    
        /// <summary>
        /// Try to load configuration from JSON file
        /// </summary>
        /// <param name="jsonPath">Path to the JSON configuration file</param>
        /// <param name="config">The loaded configuration if successful</param>
        /// <returns>True if loading and validation succeeded, false otherwise</returns>
        public static bool TryLoadFromJsonFile(string jsonPath, [NotNullWhen(true)] out MotelyJsonConfig? config)
        {
            return TryLoadFromJsonFile(jsonPath, out config, out _);
        }
        
        public static bool TryLoadFromJsonFile(string jsonPath, [NotNullWhen(true)] out MotelyJsonConfig? config, out string? error)
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
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow // Reject typos like "Valuie:" instead of "value"
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
                var baseError = $"JSON syntax error at line {jex.LineNumber}, position {jex.BytePositionInLine}: {jex.Message}";

                // Provide helpful hints for common errors
                if (jex.Message.Contains("System.Nullable`1[System.Boolean]", StringComparison.OrdinalIgnoreCase) ||
                    (jex.Message.Contains("System.Boolean", StringComparison.OrdinalIgnoreCase) &&
                     jex.Message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)))
                {
                    error = $"{baseError}\n💡 Hint: Booleans use true or false without quotes. Change \"true\" to true and \"false\" to false.\n   Example: \"requireMega\": true (correct) vs \"requireMega\": \"true\" (wrong)";
                }
                else if (jex.Message.Contains("System.String[]", StringComparison.OrdinalIgnoreCase) ||
                    (jex.Message.Contains("values", StringComparison.OrdinalIgnoreCase) &&
                     jex.Message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)))
                {
                    error = $"{baseError}\n💡 Hint: 'values' expects an array. Did you mean to use 'value' (single string) instead of 'values' (array)?\n   Example: \"value\": \"TheMagician\" or \"values\": [\"TheMagician\", \"TheHierophant\"]";
                }
                else if (jex.Message.Contains("cannot be mapped to") || jex.Message.Contains("Could not convert"))
                {
                    error = $"{baseError}\n💡 Hint: Check that array properties use [] brackets and single values don't.";
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
        private void ProcessClause(MotleyJsonFilterClause item)
        {
            DebugLogger.Log($"[PROCESS START] Type={item.Type}, Value={item.Value}, Antes={(item.Antes == null ? "null" : $"[{string.Join(",", item.Antes)}]")}, MinShop={item.MinShopSlot}, MaxShop={item.MaxShopSlot}");
            // Normalize type
            item.Type = item.Type.ToLowerInvariant();

            // Normalize arrays - but DON'T initialize flat pack/shop slots as that breaks Sources merging
            // item.PackSlots and item.ShopSlots should remain null if not provided
            item.Stickers ??= [];

            // And/Or clauses don't need antes - they inherit from nested clauses
            if (item.ItemTypeEnum != MotelyFilterItemType.And && item.ItemTypeEnum != MotelyFilterItemType.Or)
            {
                // Default to all antes if null OR empty (explicit empty array should also get default)
                if (item.Antes == null || item.Antes.Length == 0)
                    item.Antes = [1, 2, 3, 4, 5, 6, 7, 8];
            }

            // Don't initialize empty arrays - let min/max populate them later
            // if (item.Sources != null)
            // {
            //     item.Sources.PackSlots ??= [];
            //     item.Sources.ShopSlots ??= [];
            // }

            // CRITICAL: Parse all enums ONCE to avoid string operations in hot path
            item.InitializeParsedEnums();

            // Merge flat properties into Sources for backwards compatibility
            DebugLogger.Log($"[MERGE] Type={item.Type}, Value={item.Value}, flat ShopSlots={(item.ShopSlots == null ? "null" : $"[{string.Join(",", item.ShopSlots)}]")}, MinShop={item.MinShopSlot}, MaxShop={item.MaxShopSlot}");
            if (item.PackSlots != null || item.ShopSlots != null || item.RequireMega != null || item.Tags != null)
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
                    DebugLogger.Log($"[MERGE] Copying flat ShopSlots [{string.Join(",", item.ShopSlots)}] to Sources.ShopSlots");
                    item.Sources.ShopSlots = item.ShopSlots;
                }
                if (item.RequireMega != null)
                    item.Sources.RequireMega = item.RequireMega.Value;
                if (item.Tags != null)
                    item.Sources.Tags = item.Tags.Value;
            }

            // IMPORTANT: Don't apply GetDefaultSources anymore - we use ante-based defaults dynamically!
            // Only apply defaults for special cases that REQUIRE specific sources (like soul jokers pack-only)
            if (item.Sources == null && item.ItemTypeEnum != MotelyFilterItemType.And && item.ItemTypeEnum != MotelyFilterItemType.Or)
            {
                if (item.Type == "souljoker")
                {
                    // Soul jokers ONLY appear in packs, never shops
                    item.Sources = new SourcesConfig
                    {
                        ShopSlots = Array.Empty<int>(),
                        PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                        Tags = true
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
                        Tags = true
                    };
                }
                else if (item.Type is "standardcard" or "playingcard")
                {
                    // Playing cards appear in packs (shop not supported yet)
                    item.Sources = new SourcesConfig
                    {
                        ShopSlots = Array.Empty<int>(),
                        PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                        Tags = false
                    };
                }
                // ELSE: Leave Sources as null so ante-based defaults apply!
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
            if (item.Sources?.MinShopSlot.HasValue == true || item.Sources?.MaxShopSlot.HasValue == true)
            {
                int minSlot = item.Sources.MinShopSlot ?? 0;
                int maxSlot = item.Sources.MaxShopSlot ?? MotelySlotLimits.MAX_SHOP_SLOT;
                var shopSlots = new List<int>();
                for (int i = minSlot; i <= maxSlot && i <= MotelySlotLimits.MAX_SHOP_SLOT; i++)
                    shopSlots.Add(i);
                item.Sources.ShopSlots = shopSlots.ToArray();
                item.MinShopSlot = minSlot;
                item.MaxShopSlot = maxSlot;
            }
            else if (item.Sources?.ShopSlots != null && item.Sources.ShopSlots.Length > 0)
            {
                item.MinShopSlot = item.Sources.ShopSlots.Min();
                item.MaxShopSlot = item.Sources.ShopSlots.Max();
            }

            if (item.Sources?.MinPackSlot.HasValue == true || item.Sources?.MaxPackSlot.HasValue == true)
            {
                int minSlot = item.Sources.MinPackSlot ?? 0;
                int maxSlot = item.Sources.MaxPackSlot ?? MotelySlotLimits.MAX_PACK_SLOT;
                var packSlots = new List<int>();
                for (int i = minSlot; i <= maxSlot && i <= MotelySlotLimits.MAX_PACK_SLOT; i++)
                    packSlots.Add(i);
                item.Sources.PackSlots = packSlots.ToArray();
                item.MinPackSlot = minSlot;
                item.MaxPackSlot = maxSlot;
            }
            else if (item.Sources?.PackSlots != null && item.Sources.PackSlots.Length > 0)
            {
                item.MinPackSlot = item.Sources.PackSlots.Min();
                item.MaxPackSlot = item.Sources.PackSlots.Max();
            }
        }

        /// <summary>
        /// Post-process after deserialization
        /// </summary>
        public void PostProcess()
        {
            // Process all filter items recursively (handles nested And/Or clauses)
            foreach (var item in Must.Concat(Should).Concat(MustNot))
            {
                ProcessClause(item);
            }
            
            // Compute MaxVoucherAnte once during PostProcess
            int maxAnte = 0;
            if (Must != null)
            {
                foreach (var clause in Must.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher))
                {
                    if (clause.EffectiveAntes != null)
                        maxAnte = Math.Max(maxAnte, clause.EffectiveAntes.Length > 0 ? clause.EffectiveAntes.Max() : 1);
                }
            }
            if (Should != null)
            {
                foreach (var clause in Should.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher))
                {
                    if (clause.EffectiveAntes != null)
                        maxAnte = Math.Max(maxAnte, clause.EffectiveAntes.Length > 0 ? clause.EffectiveAntes.Max() : 1);
                }
            }
            MaxVoucherAnte = maxAnte;
#if DEBUG
            DebugLogger.Log($"[Config] MaxVoucherAnte calculated as: {MaxVoucherAnte}");
#endif

            // Compute MaxBossAnte once during PostProcess
            int maxBossAnte = 0;
            if (Should != null)
            {
                foreach (var clause in Should)
                {
                    if (clause.ItemTypeEnum == MotelyFilterItemType.Boss && clause.EffectiveAntes != null)
                    {
                        maxBossAnte = Math.Max(maxBossAnte, clause.EffectiveAntes.Length > 0 ? clause.EffectiveAntes.Max() : 1);
                    }
                }
            }
            MaxBossAnte = maxBossAnte;
#if DEBUG
            DebugLogger.Log($"[Config] MaxBossAnte calculated as: {MaxBossAnte}");
#endif
        }
    
        private static SourcesConfig GetDefaultSources(string itemType, string? itemValue, string deck)
        {
            return itemType switch
            {
                "souljoker" => new SourcesConfig
                {
                    ShopSlots = Array.Empty<int>(), // Legendary jokers can't appear in shops
                    PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                    Tags = true
                },
                "spectralcard" => GetSpectralCardDefaultSources(itemValue, deck),
                "tag" or "smallblindtag" or "bigblindtag" => new SourcesConfig
                {
                    ShopSlots = Array.Empty<int>(), // Tags don't appear in shop slots
                    PackSlots = Array.Empty<int>(), // Tags don't appear in pack slots
                    Tags = true
                },
                _ => new SourcesConfig
                {
                    ShopSlots = new[] { 0, 1, 2, 3 },
                    PackSlots = new[] { 0, 1, 2, 3 },
                    Tags = true
                }
            };
        }
        
        private static SourcesConfig GetSpectralCardDefaultSources(string? spectralCardValue, string deck)
        {
            // BlackHole and Soul spectral cards never appear in shop slots
            bool isBlackHoleOrSoul = !string.IsNullOrEmpty(spectralCardValue) && 
                (string.Equals(spectralCardValue, "BlackHole", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(spectralCardValue, "Soul", StringComparison.OrdinalIgnoreCase));
            
            // Other spectral cards only appear in shop slots with Ghost Deck
            bool isGhostDeck = string.Equals(deck, "Ghost", StringComparison.OrdinalIgnoreCase);
            
            return new SourcesConfig
            {
                ShopSlots = isBlackHoleOrSoul ? Array.Empty<int>() : 
                           (isGhostDeck ? new[] { 0, 1, 2, 3 } : Array.Empty<int>()),
                PackSlots = new[] { 0, 1, 2, 3 },
                Tags = true
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
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Serialize(this, options);
        }
    }