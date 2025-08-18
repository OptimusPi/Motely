using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.IO;
<<<<<<< HEAD
using System.Diagnostics;
=======
>>>>>>> master

namespace Motely.Filters
{
    /// <summary>
<<<<<<< HEAD
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
=======
>>>>>>> master
    /// MongoDB compound Operator-style Ouija configuration - Clean JSON deserialization
    /// </summary>
    public class OuijaConfig
    {
        // Metadata fields
        public string? Name { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
<<<<<<< HEAD
        public DateTime? DateCreated { get; set; }
=======
>>>>>>> master
    
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
<<<<<<< HEAD
=======
            public int? MaxAnte { get; set; }
>>>>>>> master
        }
    
        public class FilterItem
        {
            public string Type { get; set; } = "";
            public string? Value { get; set; }
        
<<<<<<< HEAD
            [JsonPropertyName("Antes")]
            public int[]? Antes { get; set; }
        
            public int Score { get; set; } = 1;
            public int? Min { get; set; }  // Minimum count required for early exit optimization
=======
        [JsonPropertyName("Antes")]
        public int[]? Antes { get; set; }
        
            public int Score { get; set; } = 1;
>>>>>>> master
            public string? Edition { get; set; }
            public List<string>? Stickers { get; set; }
        
            // PlayingCard specific
            public string? Suit { get; set; }
            public string? Rank { get; set; }
            public string? Seal { get; set; }
            public string? Enhancement { get; set; }
        
            // Sources configuration
            public SourcesConfig? Sources { get; set; }
<<<<<<< HEAD
            
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
                        _cachedItemTypeEnum = Type?.ToLowerInvariant() switch
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
                    }
                    return _cachedItemTypeEnum.Value;
                }
            }
        
            // Pre-parsed enum values - parsed ONCE at initialization, not in hot path!
            [JsonIgnore]
            public JokerWildcard? WildcardEnum { get; private set; }
            
            [JsonIgnore]
            public MotelyJoker? JokerEnum { get; private set; }
            
            // Call this after deserialization to parse all enums ONCE
            public void InitializeParsedEnums()
            {
                Debug.WriteLine($"[InitializeParsedEnums] Called for Type='{Type}', Value='{Value}'");
                
                // Parse based on item type
                switch (ItemTypeEnum)
                {
                    case MotelyFilterItemType.Joker:
                    case MotelyFilterItemType.SoulJoker:  // ALSO parse joker enum for soul jokers!
                        // Parse wildcard first (only for regular jokers, not soul jokers)
                        if (ItemTypeEnum == MotelyFilterItemType.Joker)
                        {
                            WildcardEnum = Value?.ToLowerInvariant() switch
                            {
                                "any" => JokerWildcard.AnyJoker,  // Support both "any" and "anyjoker"
                                "anyjoker" => JokerWildcard.AnyJoker,
                                "anycommon" => JokerWildcard.AnyCommon,
                                "anyuncommon" => JokerWildcard.AnyUncommon,
                                "anyrare" => JokerWildcard.AnyRare,
                                "anylegendary" => JokerWildcard.AnyLegendary,
                                _ => null
                            };
                        }
                        
                        Debug.WriteLine($"[InitializeParsedEnums] Joker parsing: WildcardEnum={WildcardEnum}");
                        
                        // Only parse as joker enum if it's not a wildcard
                        if (!WildcardEnum.HasValue && !string.IsNullOrEmpty(Value))
                        {
                            if (Enum.TryParse<MotelyJoker>(Value, true, out var joker))
                            {
                                JokerEnum = joker;
                                Debug.WriteLine($"[InitializeParsedEnums] Successfully parsed joker: '{Value}' -> {JokerEnum} (int value: {(int)joker})");
                                
                                // IMPORTANT DEBUG: Log if this is a Legendary joker
                                if (Value.Equals("Perkeo", StringComparison.OrdinalIgnoreCase) || 
                                    Value.Equals("Triboulet", StringComparison.OrdinalIgnoreCase))
                                {
                                    Debug.WriteLine($"[InitializeParsedEnums] LEGENDARY JOKER PARSED: '{Value}' -> Enum={JokerEnum}, IntValue={(int)joker}");
                                }
                            }
                            else
                            {
                                Debug.WriteLine($"[InitializeParsedEnums] FAILED to parse joker value: '{Value}'");
                                // In debug mode, crash if we can't parse a joker value
                                Debug.Assert(false, $"Failed to parse joker value: '{Value}'. This is not a valid MotelyJoker enum value!");
                            }
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
                        
                    case MotelyFilterItemType.Voucher:
                        if (!string.IsNullOrEmpty(Value))
                        {
                            if (Enum.TryParse<MotelyVoucher>(Value, true, out var voucher))
                            {
                                VoucherEnum = voucher;
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
            
            [JsonIgnore]
            public MotelyVoucher? VoucherEnum { get; private set; }
            
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
            
=======
        
        // Get effective antes; default to antes 1-8 when unspecified
        [JsonIgnore]
        public int[] EffectiveAntes => Antes ?? new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        
            // === COMPUTED ENUM PROPERTIES FOR COMPATIBILITY ===
            // These parse on-demand from the string values
        
            [JsonIgnore]
            public MotelyFilterItemType? ItemTypeEnum => Type?.ToLowerInvariant() switch
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
                _ => null
            };
        
            [JsonIgnore]
            public MotelyJoker? JokerEnum => 
                !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyJoker>(Value, true, out var joker) ? joker : null;
        
            [JsonIgnore]
            public MotelyTarotCard? TarotEnum => 
                !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyTarotCard>(Value, true, out var tarot) ? tarot : null;
        
            [JsonIgnore]
            public MotelySpectralCard? SpectralEnum => 
                !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelySpectralCard>(Value, true, out var spectral) ? spectral : null;
        
            [JsonIgnore]
            public MotelyPlanetCard? PlanetEnum => 
                !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyPlanetCard>(Value, true, out var planet) ? planet : null;
        
            [JsonIgnore]
            public MotelyTag? TagEnum => 
                !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyTag>(Value, true, out var tag) ? tag : null;
        
            [JsonIgnore]
            public MotelyVoucher? VoucherEnum => 
                !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyVoucher>(Value, true, out var voucher) ? voucher : null;
        
            [JsonIgnore]
            public MotelyBossBlind? BossEnum => 
                !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyBossBlind>(Value, true, out var boss) ? boss : null;
        
            [JsonIgnore]
            public MotelyItemEdition? EditionEnum => 
                !string.IsNullOrEmpty(Edition) && Enum.TryParse<MotelyItemEdition>(Edition, true, out var ed) ? ed : null;
        
            [JsonIgnore]
            public MotelyPlayingCardRank? RankEnum => 
                !string.IsNullOrEmpty(Rank) && Enum.TryParse<MotelyPlayingCardRank>(Rank, true, out var rank) ? rank : null;
        
            [JsonIgnore]
            public MotelyPlayingCardSuit? SuitEnum => 
                !string.IsNullOrEmpty(Suit) && Enum.TryParse<MotelyPlayingCardSuit>(Suit, true, out var suit) ? suit : null;
        
            [JsonIgnore]
            public MotelyItemEnhancement? EnhancementEnum => 
                !string.IsNullOrEmpty(Enhancement) && Enum.TryParse<MotelyItemEnhancement>(Enhancement, true, out var enh) ? enh : null;
        
            [JsonIgnore]
            public MotelyItemSeal? SealEnum => 
                !string.IsNullOrEmpty(Seal) && Enum.TryParse<MotelyItemSeal>(Seal, true, out var seal) ? seal : null;
        
>>>>>>> master
            [JsonIgnore]
            public List<MotelyJokerSticker>? StickerEnums
            {
                get
                {
<<<<<<< HEAD
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
=======
                    if (Stickers == null || Stickers.Count == 0)
                    {
                        return null;
                    }

                    var result = new List<MotelyJokerSticker>();
                    foreach (var sticker in Stickers)
                    {
                        if (Enum.TryParse<MotelyJokerSticker>(sticker, true, out var s))
                        {
                            result.Add(s);
                        }
                    }
                    return result.Count > 0 ? result : null;
                }
            }
        
            [JsonIgnore]
            public MotelyTagType? TagTypeEnum => Type?.ToLowerInvariant() switch
            {
                "smallblindtag" => MotelyTagType.SmallBlind,
                "bigblindtag" => MotelyTagType.BigBlind,
                _ => MotelyTagType.Any
            };
>>>>>>> master
        
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
        /// Load from JSON file
        /// </summary>
        public static OuijaConfig LoadFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"Config file not found: {jsonPath}");
            }

            var json = File.ReadAllText(jsonPath);
        
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var config = JsonSerializer.Deserialize<OuijaConfig>(json, options) 
                ?? throw new InvalidOperationException("Failed to parse config");
            
            config.PostProcess();
            return config;
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
<<<<<<< HEAD
                
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
=======
>>>>>>> master
            
                // Set default sources if not specified
                if (item.Sources == null)
                {
                    item.Sources = GetDefaultSources(item.Type);
                }
<<<<<<< HEAD
                
                // Override sources for special items
                OverrideSpecialItemSources(item);
=======
>>>>>>> master
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
<<<<<<< HEAD
        
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
        }
=======
>>>>>>> master
    
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