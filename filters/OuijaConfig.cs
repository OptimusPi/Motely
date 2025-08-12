using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace Motely.Filters
{
    /// <summary>
    /// MongoDB compound Operator-style Ouija configuration - Clean JSON deserialization
    /// </summary>
    public class OuijaConfig
    {
        // Metadata fields
        public string? Name { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
    
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
            public int? MaxAnte { get; set; }
        }
    
        public class FilterItem
        {
            public string Type { get; set; } = "";
            public string? Value { get; set; }
        
            [JsonPropertyName("Antes")]
            public int[]? Antes { get; set; }
        
            public int Score { get; set; } = 1;
            public string? Edition { get; set; }
            public List<string>? Stickers { get; set; }
        
            // PlayingCard specific
            public string? Suit { get; set; }
            public string? Rank { get; set; }
            public string? Seal { get; set; }
            public string? Enhancement { get; set; }
        
            // Sources configuration
            public SourcesConfig? Sources { get; set; }
        
        // Get effective antes; default to antes 1-8 when unspecified
        [JsonIgnore]
        public int[] EffectiveAntes => Antes ?? new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        
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
        
            // Cache all parsed enum values to avoid string operations in hot path
            private MotelyJoker? _cachedJokerEnum;
            private bool _jokerEnumParsed;
            
            [JsonIgnore]
            public MotelyJoker? JokerEnum
            {
                get
                {
                    if (!_jokerEnumParsed)
                    {
                        _cachedJokerEnum = !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyJoker>(Value, true, out var joker) ? joker : null;
                        _jokerEnumParsed = true;
                    }
                    return _cachedJokerEnum;
                }
            }
            
            private MotelyTarotCard? _cachedTarotEnum;
            private bool _tarotEnumParsed;
            
            [JsonIgnore]
            public MotelyTarotCard? TarotEnum
            {
                get
                {
                    if (!_tarotEnumParsed)
                    {
                        _cachedTarotEnum = !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyTarotCard>(Value, true, out var tarot) ? tarot : null;
                        _tarotEnumParsed = true;
                    }
                    return _cachedTarotEnum;
                }
            }
            
            private MotelySpectralCard? _cachedSpectralEnum;
            private bool _spectralEnumParsed;
            
            [JsonIgnore]
            public MotelySpectralCard? SpectralEnum
            {
                get
                {
                    if (!_spectralEnumParsed)
                    {
                        _cachedSpectralEnum = !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelySpectralCard>(Value, true, out var spectral) ? spectral : null;
                        _spectralEnumParsed = true;
                    }
                    return _cachedSpectralEnum;
                }
            }
            
            private MotelyPlanetCard? _cachedPlanetEnum;
            private bool _planetEnumParsed;
            
            [JsonIgnore]
            public MotelyPlanetCard? PlanetEnum
            {
                get
                {
                    if (!_planetEnumParsed)
                    {
                        _cachedPlanetEnum = !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyPlanetCard>(Value, true, out var planet) ? planet : null;
                        _planetEnumParsed = true;
                    }
                    return _cachedPlanetEnum;
                }
            }
            
            private MotelyTag? _cachedTagEnum;
            private bool _tagEnumParsed;
            
            [JsonIgnore]
            public MotelyTag? TagEnum
            {
                get
                {
                    if (!_tagEnumParsed)
                    {
                        _cachedTagEnum = !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyTag>(Value, true, out var tag) ? tag : null;
                        _tagEnumParsed = true;
                    }
                    return _cachedTagEnum;
                }
            }
            
            private MotelyVoucher? _cachedVoucherEnum;
            private bool _voucherEnumParsed;
            
            [JsonIgnore]
            public MotelyVoucher? VoucherEnum
            {
                get
                {
                    if (!_voucherEnumParsed)
                    {
                        _cachedVoucherEnum = !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyVoucher>(Value, true, out var voucher) ? voucher : null;
                        _voucherEnumParsed = true;
                    }
                    return _cachedVoucherEnum;
                }
            }
            
            private MotelyBossBlind? _cachedBossEnum;
            private bool _bossEnumParsed;
            
            [JsonIgnore]
            public MotelyBossBlind? BossEnum
            {
                get
                {
                    if (!_bossEnumParsed)
                    {
                        _cachedBossEnum = !string.IsNullOrEmpty(Value) && Enum.TryParse<MotelyBossBlind>(Value, true, out var boss) ? boss : null;
                        _bossEnumParsed = true;
                    }
                    return _cachedBossEnum;
                }
            }
            
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
                
                // CRITICAL: Parse all enums ONCE to avoid string operations in hot path
                item.CompileEnums();
            
                // Set default sources if not specified
                if (item.Sources == null)
                {
                    item.Sources = GetDefaultSources(item.Type);
                }
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