using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Motely.Filters
{
    /// <summary>
    /// Wildcard types for joker filtering
    /// </summary>
    public enum JokerWildcard
    {
        AnyJoker,
        AnyCommon,
        AnyUncommon,
        AnyRare,
        AnyLegendary
    }

    /// <summary>
    /// MongoDB compound Operator-style Ouija configuration - Clean JSON deserialization
    /// </summary>
    public class OuijaConfig
    {
        // Metadata fields
        public string? Name { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public DateTime? DateCreated { get; set; }
    
        public List<FilterItem> Must { get; set; } = new();
        public List<FilterItem> Should { get; set; } = new();
        public List<FilterItem> MustNot { get; set; } = new();
    
        // Optional deck/stake settings
        public string Deck { get; set; } = "Red";
        public string Stake { get; set; } = "White";
    
        // Support nested filter format
        public FilterSettings? Filter { get; set; }
    
        public class FilterSettings
        {
            public string? Deck { get; set; }
            public string? Stake { get; set; }
        }
    
        public class FilterItem
        {
            public string Type { get; set; } = "";
            public string? Value { get; set; }
        
            [JsonPropertyName("Antes")]
            public int[]? Antes { get; set; }
        
            public int Score { get; set; } = 1;
            public int? Min { get; set; }  // Minimum count required for early exit optimization
            public string? Edition { get; set; }
            public List<string>? Stickers { get; set; }
        
            // PlayingCard specific
            public string? Suit { get; set; }
            public string? Rank { get; set; }
            public string? Seal { get; set; }
            public string? Enhancement { get; set; }
        
            // Sources configuration
            public SourcesConfig? Sources { get; set; }
            
            // Cached slot membership bitmasks (built in PostProcess). Up to 32 slots supported.
            // Bit i set => slot i included.
            internal uint PackSlotMask; // bitmask for requested pack slots
            internal uint ShopSlotMask; // bitmask for requested shop slots
            public bool HasPackSlot(int slot) => slot >= 0 && slot < 32 && ((PackSlotMask >> slot) & 1u) != 0u;
            public bool HasShopSlot(int slot) => slot >= 0 && slot < 32 && ((ShopSlotMask >> slot) & 1u) != 0u;

            // Precomputed metadata flags (populated in PostProcess after sources merged)
            internal int MaxPackSlot = -1; // highest referenced pack slot (or -1 if none)
            internal int MaxShopSlot = -1; // highest referenced shop slot (or -1 if none)
            internal bool IsWildcardAnyJoker; // true if wildcard and AnyJoker
            internal bool IsSimpleNegativeAnyPackOnly; // pattern: joker, AnyJoker, Edition specified, only packSlots, no shopSlots, Min null/<=1
            
            // Direct properties for backwards compatibility and simpler JSON
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
            get
            {
                if (Antes == null || Antes.Length == 0)
                {
                    // Default to all 8 antes if not specified
                    return new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };
                }
                return Antes;
            }
        }
        
            // === COMPUTED ENUM PROPERTIES FOR COMPATIBILITY ===
            // Cache parsed enum values to avoid string operations in hot path
            
            private MotelyFilterItemType? _cachedItemTypeEnum;
            
            [JsonIgnore]
            public MotelyFilterItemType ItemTypeEnum
            {
                get
                {
                    // Cache the parsed enum value to avoid string operations in hot path
                    if (!_cachedItemTypeEnum.HasValue)
                    {
                        var typeEnum = Type?.ToLowerInvariant() switch
                        {
                            // Strict canonical identifiers only; case-insensitive, no aliases
                            "joker" => MotelyFilterItemType.Joker,
                            "souljoker" => MotelyFilterItemType.SoulJoker,
                            "tarotcard" => MotelyFilterItemType.TarotCard,
                            "planetcard" => MotelyFilterItemType.PlanetCard,
                            "spectralcard" => MotelyFilterItemType.SpectralCard,
                            "smallblindtag" => MotelyFilterItemType.SmallBlindTag,
                            "bigblindtag" => MotelyFilterItemType.BigBlindTag,
                            "voucher" => MotelyFilterItemType.Voucher,
                            "playingcard" => MotelyFilterItemType.PlayingCard,
                            "boss" => MotelyFilterItemType.Boss,
                            _ => throw new ArgumentException($"Unknown filter item type: {Type}.")
                        };
                        
                        // Auto-convert legendary joker names and wildcards from "joker" to "souljoker" type
                        if (typeEnum == MotelyFilterItemType.Joker && !string.IsNullOrEmpty(Value))
                        {
                            var valueLower = Value.ToLowerInvariant();
                            if (valueLower == "perkeo" || valueLower == "canio" || valueLower == "triboulet" || 
                                valueLower == "chicot" || valueLower == "yorick")
                            {
                                typeEnum = MotelyFilterItemType.SoulJoker;
                            }
                        }
                        
                        _cachedItemTypeEnum = typeEnum;
                    }
                    return _cachedItemTypeEnum.Value;
                }
            }
        
            // Pre-parsed enum values - parsed ONCE at initialization, not in hot path!
            [JsonIgnore]
            public JokerWildcard? WildcardEnum { get; private set; }
            
            [JsonIgnore]
            public MotelyJoker? JokerEnum { get; private set; }
            [JsonIgnore]
            public MotelyVoucher? VoucherEnum { get; private set; }
            
            // Cached target rarity for wildcard searches - computed once at config load time
            [JsonIgnore]
            public MotelyJokerRarity? CachedTargetRarity { get; private set; }
            
            // Call this after deserialization to parse all enums ONCE
            public void InitializeParsedEnums()
            {
                Debug.WriteLine($"[InitializeParsedEnums] Called for Type='{Type}', Value='{Value}'");
                
                // Parse based on item type
                switch (ItemTypeEnum)
                {
                    case MotelyFilterItemType.Joker:
                    case MotelyFilterItemType.SoulJoker:
                        // Set WildcardEnum for Joker types
                        if (ItemTypeEnum == MotelyFilterItemType.Joker)
                        {
                            WildcardEnum = Value?.ToLowerInvariant() switch
                            {
                                "any" => JokerWildcard.AnyJoker,
                                "anyjoker" => JokerWildcard.AnyJoker,
                                "anycommon" => JokerWildcard.AnyCommon,
                                "anyuncommon" => JokerWildcard.AnyUncommon,
                                "anyrare" => JokerWildcard.AnyRare,
                                "anylegendary" => JokerWildcard.AnyLegendary,
                                _ => null
                            };
                        }
                        // Set WildcardEnum for SoulJoker types (no "anylegendary" since all soul jokers are legendary)
                        else if (ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                        {
                            WildcardEnum = Value?.ToLowerInvariant() switch
                            {
                                "any" => JokerWildcard.AnyJoker,
                                "anyjoker" => JokerWildcard.AnyJoker,
                                "anycommon" => JokerWildcard.AnyCommon,
                                "anyuncommon" => JokerWildcard.AnyUncommon,
                                "anyrare" => JokerWildcard.AnyRare,
                                // Note: "anylegendary" is not supported for soul jokers because all soul jokers are legendary
                                _ => null
                            };
                        }
                        Debug.WriteLine($"[InitializeParsedEnums] Joker parsing: WildcardEnum={WildcardEnum}");
                        
                        // Pre-compute target rarity for wildcard searches to avoid switch in hot path
                        if (WildcardEnum.HasValue)
                        {
                            CachedTargetRarity = WildcardEnum switch
                            {
                                JokerWildcard.AnyCommon => MotelyJokerRarity.Common,
                                JokerWildcard.AnyUncommon => MotelyJokerRarity.Uncommon,
                                JokerWildcard.AnyRare => MotelyJokerRarity.Rare,
                                JokerWildcard.AnyLegendary => MotelyJokerRarity.Legendary,
                    
                                JokerWildcard.AnyJoker => null, // Any rarity
                                _ => null
                            };
                        }
                        
                        if (!WildcardEnum.HasValue && !string.IsNullOrEmpty(Value))
                        {
                            if (Enum.TryParse<MotelyJoker>(Value, true, out var joker))
                            {
                                JokerEnum = joker;
                                if (Value.Equals("Perkeo", StringComparison.OrdinalIgnoreCase) || Value.Equals("Triboulet", StringComparison.OrdinalIgnoreCase))
                                {
                                    Debug.WriteLine($"[InitializeParsedEnums] LEGENDARY JOKER PARSED: '{Value}' -> Enum={JokerEnum}, IntValue={(int)joker}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[InitializeParsedEnums] FAILED to parse joker value: '{Value}'");
                                Debug.Assert(false, $"Failed to parse joker value: '{Value}'. This is not a valid MotelyJoker enum value!");
                            }
                        }
                        break;
                    case MotelyFilterItemType.Voucher:
                        if (!string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyVoucher>(Value, true, out var voucher))
                        {
                            VoucherEnum = voucher;
                            Debug.WriteLine($"[InitializeParsedEnums] Parsed voucher value '{Value}' -> {voucher}");
                        }
                        else
                        {
                            Debug.WriteLine($"[InitializeParsedEnums] FAILED to parse voucher value: '{Value}' (IsNullOrEmpty={string.IsNullOrEmpty(Value)})");
                        }
                        break;
                    case MotelyFilterItemType.TarotCard:
                        if (!string.IsNullOrEmpty(Value))
                        {
                            if (Enum.TryParse<MotelyTarotCard>(Value, true, out var tarot))
                            {
                                TarotEnum = tarot;
                            }
                        }
                        break;
                        
                    case MotelyFilterItemType.SpectralCard:
                        if (!string.IsNullOrEmpty(Value))
                        {
                            if (Enum.TryParse<MotelySpectralCard>(Value, true, out var spectral))
                            {
                                SpectralEnum = spectral;
                            }
                        }
                        break;
                        
                    case MotelyFilterItemType.PlanetCard:
                        if (!string.IsNullOrEmpty(Value))
                        {
                            if (Enum.TryParse<MotelyPlanetCard>(Value, true, out var planet))
                            {
                                PlanetEnum = planet;
                            }
                        }
                        break;
                        
                    case MotelyFilterItemType.SmallBlindTag:
                    case MotelyFilterItemType.BigBlindTag:
                        if (!string.IsNullOrEmpty(Value))
                        {
                            if (Enum.TryParse<MotelyTag>(Value, true, out var tag))
                            {
                                TagEnum = tag;
                            }
                        }
                        break;
                        
                    case MotelyFilterItemType.Boss:
                        if (!string.IsNullOrEmpty(Value))
                        {
                            if (Enum.TryParse<MotelyBossBlind>(Value, true, out var boss))
                            {
                                BossEnum = boss;
                            }
                        }
                        break;
                }
            }
            
            [JsonIgnore]
            public MotelyTarotCard? TarotEnum { get; private set; }
            
            [JsonIgnore]
            public MotelySpectralCard? SpectralEnum { get; private set; }
            
            [JsonIgnore]
            public MotelyPlanetCard? PlanetEnum { get; private set; }
            
            [JsonIgnore]
            public MotelyTag? TagEnum { get; private set; }
            
            // VoucherEnum already declared above
            
            [JsonIgnore]
            public MotelyBossBlind? BossEnum { get; private set; }
            
            private MotelyItemEdition? _cachedEditionEnum;
            private bool _editionEnumParsed;
            
            [JsonIgnore]
            public MotelyItemEdition? EditionEnum
            {
                get
                {
                    if (!_editionEnumParsed)
                    {
                        _cachedEditionEnum = !string.IsNullOrEmpty(Edition) && Enum.TryParse<MotelyItemEdition>(Edition, true, out var ed) ? ed : null;
                        _editionEnumParsed = true;
                    }
                    return _cachedEditionEnum;
                }
            }
            
            private MotelyPlayingCardRank? _cachedRankEnum;
            private bool _rankEnumParsed;
            
            [JsonIgnore]
            public MotelyPlayingCardRank? RankEnum
            {
                get
                {
                    if (!_rankEnumParsed)
                    {
                        _cachedRankEnum = !string.IsNullOrEmpty(Rank) && Enum.TryParse<MotelyPlayingCardRank>(Rank, true, out var rank) ? rank : null;
                        _rankEnumParsed = true;
                    }
                    return _cachedRankEnum;
                }
            }
            
            private MotelyPlayingCardSuit? _cachedSuitEnum;
            private bool _suitEnumParsed;
            
            [JsonIgnore]
            public MotelyPlayingCardSuit? SuitEnum
            {
                get
                {
                    if (!_suitEnumParsed)
                    {
                        _cachedSuitEnum = !string.IsNullOrEmpty(Suit) && Enum.TryParse<MotelyPlayingCardSuit>(Suit, true, out var suit) ? suit : null;
                        _suitEnumParsed = true;
                    }
                    return _cachedSuitEnum;
                }
            }
            
            private MotelyItemEnhancement? _cachedEnhancementEnum;
            private bool _enhancementEnumParsed;
            
            [JsonIgnore]
            public MotelyItemEnhancement? EnhancementEnum
            {
                get
                {
                    if (!_enhancementEnumParsed)
                    {
                        _cachedEnhancementEnum = !string.IsNullOrEmpty(Enhancement) && Enum.TryParse<MotelyItemEnhancement>(Enhancement, true, out var enh) ? enh : null;
                        _enhancementEnumParsed = true;
                    }
                    return _cachedEnhancementEnum;
                }
            }
            
            private MotelyItemSeal? _cachedSealEnum;
            private bool _sealEnumParsed;
            
            [JsonIgnore]
            public MotelyItemSeal? SealEnum
            {
                get
                {
                    if (!_sealEnumParsed)
                    {
                        _cachedSealEnum = !string.IsNullOrEmpty(Seal) && Enum.TryParse<MotelyItemSeal>(Seal, true, out var seal) ? seal : null;
                        _sealEnumParsed = true;
                    }
                    return _cachedSealEnum;
                }
            }
            
            /// <summary>
            /// Force all enums to be parsed immediately to avoid string operations in hot path
            /// </summary>
            internal void CompileEnums()
            {
                // Access each enum property to force parsing
                _ = ItemTypeEnum;
                _ = JokerEnum;
                _ = TarotEnum;
                _ = SpectralEnum;
                _ = PlanetEnum;
                _ = TagEnum;
                _ = VoucherEnum;
                _ = BossEnum;
                _ = EditionEnum;
                _ = RankEnum;
                _ = SuitEnum;
                _ = EnhancementEnum;
                _ = SealEnum;
                _ = StickerEnums;
                _ = TagTypeEnum;
            }
        
            private List<MotelyJokerSticker>? _cachedStickerEnums;
            private bool _stickerEnumsParsed;
            
            [JsonIgnore]
            public List<MotelyJokerSticker>? StickerEnums
            {
                get
                {
                    if (!_stickerEnumsParsed)
                    {
                        if (Stickers == null || Stickers.Count == 0)
                        {
                            _cachedStickerEnums = null;
                        }
                        else
                        {
                            var result = new List<MotelyJokerSticker>();
                            foreach (var sticker in Stickers)
                            {
                                if (Enum.TryParse<MotelyJokerSticker>(sticker, true, out var s))
                                {
                                    result.Add(s);
                                }
                            }
                            _cachedStickerEnums = result.Count > 0 ? result : null;
                        }
                        _stickerEnumsParsed = true;
                    }
                    return _cachedStickerEnums;
                }
            }
            
            private MotelyTagType? _cachedTagTypeEnum;
            private bool _tagTypeEnumParsed;
        
            [JsonIgnore]
            public MotelyTagType? TagTypeEnum
            {
                get
                {
                    if (!_tagTypeEnumParsed)
                    {
                        _cachedTagTypeEnum = Type?.ToLowerInvariant() switch
                        {
                            "smallblindtag" => MotelyTagType.SmallBlind,
                            "bigblindtag" => MotelyTagType.BigBlind,
                            _ => MotelyTagType.Any
                        };
                        _tagTypeEnumParsed = true;
                    }
                    return _cachedTagTypeEnum;
                }
            }
        
        // No rank normalization: require canonical enum names (case-insensitive only)
        }
    
        public class SourcesConfig
        {
            [JsonPropertyName("shopSlots")]
            public int[]? ShopSlots { get; set; }
        
            [JsonPropertyName("packSlots")]
            public int[]? PackSlots { get; set; }
        
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
        public static bool TryLoadFromJsonFile(string jsonPath, [NotNullWhen(true)] out OuijaConfig? config)
        {
            config = null;
            
            if (!File.Exists(jsonPath))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(jsonPath);
            
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var deserializedConfig = JsonSerializer.Deserialize<OuijaConfig>(json, options);
                if (deserializedConfig == null)
                {
                    return false;
                }
                
                deserializedConfig.PostProcess();
                
                // Validate config
                OuijaConfigValidator.ValidateConfig(deserializedConfig);
                
                config = deserializedConfig;
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Config loading failed for {jsonPath}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Load from JSON file - returns null if validation fails
        /// </summary>
        public static OuijaConfig? LoadFromJson(string jsonPath)
        {
            return TryLoadFromJsonFile(jsonPath, out var config) ? config : null;
        }
    
        /// <summary>
        /// Post-process after deserialization
        /// </summary>
        private void PostProcess()
        {
            // Apply nested filter settings if present
            if (Filter != null)
            {
                if (!string.IsNullOrEmpty(Filter.Deck))
                {
                    Deck = Filter.Deck;
                }

                if (!string.IsNullOrEmpty(Filter.Stake))
                {
                    Stake = Filter.Stake;
                }
            }

            // Process all filter items
            foreach (var item in Must.Concat(Should).Concat(MustNot))
            {
                // Normalize type
                item.Type = item.Type.ToLowerInvariant();
                
                // CRITICAL: Parse all enums ONCE to avoid string operations in hot path
                item.InitializeParsedEnums();
            
                // Merge flat properties into Sources for backwards compatibility
                if (item.PackSlots != null || item.ShopSlots != null || item.RequireMega != null || item.Tags != null)
                {
                    if (item.Sources == null)
                    {
                        item.Sources = new SourcesConfig();
                    }
                    
                    // Use flat properties if provided, otherwise keep existing Sources values
                    if (item.PackSlots != null)
                        item.Sources.PackSlots = item.PackSlots;
                    if (item.ShopSlots != null)
                        item.Sources.ShopSlots = item.ShopSlots;
                    if (item.RequireMega != null)
                        item.Sources.RequireMega = item.RequireMega.Value;
                    if (item.Tags != null)
                        item.Sources.Tags = item.Tags.Value;
                }
            
                // Set default sources if not specified
                if (item.Sources == null)
                {
                    item.Sources = GetDefaultSources(item.Type);
                }
                
                // Override sources for special items
                OverrideSpecialItemSources(item);
            }

            // Second pass: compute per-item metadata (after overrides & masks) 
            foreach (var item in Must.Concat(Should).Concat(MustNot))
            {
                if (item.Sources?.PackSlots != null && item.Sources.PackSlots.Length > 0)
                    item.MaxPackSlot = item.Sources.PackSlots.Max();
                if (item.Sources?.ShopSlots != null && item.Sources.ShopSlots.Length > 0)
                    item.MaxShopSlot = item.Sources.ShopSlots.Max();

                item.IsWildcardAnyJoker = item.ItemTypeEnum == MotelyFilterItemType.Joker && item.WildcardEnum == JokerWildcard.AnyJoker;
                bool onlyPacks = (item.Sources?.PackSlots?.Length ?? 0) > 0 && (item.Sources?.ShopSlots?.Length ?? 0) == 0;
                bool simpleMin = !item.Min.HasValue || item.Min.Value <= 1;
                item.IsSimpleNegativeAnyPackOnly = item.IsWildcardAnyJoker && onlyPacks && simpleMin && item.EditionEnum.HasValue;
            }
        }
    
        private static SourcesConfig GetDefaultSources(string itemType)
        {
            return itemType switch
            {
                "souljoker" => new SourcesConfig
                {
                    ShopSlots = Array.Empty<int>(), // Legendary jokers can't appear in shops
                    PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                    Tags = true
                },
                "boss" or "bossblind" => new SourcesConfig
                {
                    ShopSlots = Array.Empty<int>(),
                    PackSlots = Array.Empty<int>(),
                    Tags = false
                },
                _ => new SourcesConfig
                {
                    ShopSlots = new[] { 0, 1, 2, 3, 4, 5 },
                    PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                    Tags = true
                }
            };
        }
        
        // Override sources for special items that can't appear in shops
        private void OverrideSpecialItemSources(FilterItem item)
        {
            // Soul and BlackHole special cards (0.3% chance in packs):
            // - Soul: Tarot in Arcana packs (gives legendary joker), Spectral in Spectral packs
            // - BlackHole: Planet in Celestial packs (upgrades 3 planets), Spectral in Spectral packs
            // They NEVER appear in shops - only in packs!
            if (item.ItemTypeEnum == MotelyFilterItemType.SpectralCard)
            {
                if (item.SpectralEnum == MotelySpectralCard.Soul || 
                    item.SpectralEnum == MotelySpectralCard.BlackHole)
                {
                    // Only override ShopSlots (they can NEVER appear in shops)
                    // Keep user's packSlots and requireMega settings!
                    if (item.Sources != null)
                    {
                        item.Sources.ShopSlots = Array.Empty<int>(); // Cannot appear in shops
                        item.Sources.Tags = false; // Can't come from tags either
                    }
                    else
                    {
                        item.Sources = new SourcesConfig
                        {
                            ShopSlots = Array.Empty<int>(),
                            PackSlots = new[] { 0, 1, 2, 3, 4, 5 },
                            Tags = false
                        };
                    }
                }
            }

            // Build cached bitmasks for fast slot membership tests (tolerate duplicates)
            // NOTE: Previously this executed ONLY for SpectralCard due to scoping bug; now applies to ALL item types.
            if (item.Sources?.PackSlots != null)
            {
                uint packMask = 0;
                foreach (var s in item.Sources.PackSlots)
                    if (s >= 0 && s < 32) packMask |= (1u << s);
                item.PackSlotMask = packMask;
            }
            if (item.Sources?.ShopSlots != null)
            {
                uint shopMask = 0;
                foreach (var s in item.Sources.ShopSlots)
                    if (s >= 0 && s < 32) shopMask |= (1u << s);
                item.ShopSlotMask = shopMask;
            }
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


    // Strict mode: legacy sources formats (like string arrays) are not supported.
}