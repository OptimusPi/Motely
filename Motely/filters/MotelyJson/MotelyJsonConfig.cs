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
    /// MongoDB compound Operator-style JSON configuration
    /// </summary>
    public class MotelyJsonConfig
    {
        // Metadata fields
        public string? Name { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public DateTime? DateCreated { get; set; }
    
        public List<MotleyJsonFilterClause> Must { get; set; } = new();
        public List<MotleyJsonFilterClause> Should { get; set; } = new();
        public List<MotleyJsonFilterClause> MustNot { get; set; } = new();
        
    public class MotleyJsonFilterClause
    {
        public string Type { get; set; } = "";
        public string? Value { get; set; }
        public string? Label { get; set; } // Custom label for CSV column headers
        public int[]? Antes { get; set; }
        
        public int Score { get; set; } = 1;
        public int? Min { get; set; }
        public string? Edition { get; set; }
        public List<string>? Stickers { get; set; }
        
        // PlayingCard specific
        public string? Suit { get; set; }
        public string? Rank { get; set; }
        public string? Seal { get; set; }
        public string? Enhancement { get; set; }
        
        // Sources configuration
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
            get
            {
                return Antes;
            }

            set
            {
                Antes = value;
            }
        }
        
        // Pre-computed values (set during initialization)
        public int? MaxShopSlot { get; set; }
        public int? MaxPackSlot { get; set; }
        
        // Parsed enums (cached for performance)
        private MotelyFilterItemType? _cachedItemTypeEnum;
        
        [JsonIgnore]
        public MotelyFilterItemType ItemTypeEnum
        {
            get
            {
                if (!_cachedItemTypeEnum.HasValue)
                {
                    var typeEnum = Type?.ToLowerInvariant() switch
                    {
                        "joker" => MotelyFilterItemType.Joker,
                        "souljoker" => MotelyFilterItemType.SoulJoker,
                        "tarot" or "tarotcard" => MotelyFilterItemType.TarotCard,
                        "planet" or "planetcard" => MotelyFilterItemType.PlanetCard,
                        "spectral" or "spectralcard" => MotelyFilterItemType.SpectralCard,
                        "smallblindtag" => MotelyFilterItemType.SmallBlindTag,
                        "bigblindtag" => MotelyFilterItemType.BigBlindTag,
                        "voucher" => MotelyFilterItemType.Voucher,
                        "playingcard" => MotelyFilterItemType.PlayingCard,
                        "boss" => MotelyFilterItemType.Boss,
                        _ => throw new ArgumentException($"Unknown filter item type: {Type}")
                    };
                    _cachedItemTypeEnum = typeEnum;
                }
                return _cachedItemTypeEnum.Value;
            }
        }
        
        [JsonIgnore] public MotelyVoucher? VoucherEnum { get; set; }
        [JsonIgnore] public MotelyTarotCard? TarotEnum { get; set; }
        [JsonIgnore] public MotelyPlanetCard? PlanetEnum { get; set; }
        [JsonIgnore] public MotelySpectralCard? SpectralEnum { get; set; }
        [JsonIgnore] public MotelyJoker? JokerEnum { get; set; }
        [JsonIgnore] public MotelyTag? TagEnum { get; set; }
        [JsonIgnore] public MotelyTagType TagTypeEnum { get; set; }
        [JsonIgnore] public MotelyBossBlind? BossEnum { get; set; }
        [JsonIgnore] public MotelyItemEdition? EditionEnum { get; set; }
        [JsonIgnore] public List<MotelyJokerSticker>? StickerEnums { get; set; }
        [JsonIgnore] public MotelyPlayingCardSuit? SuitEnum { get; set; }
        [JsonIgnore] public MotelyPlayingCardRank? RankEnum { get; set; }
        [JsonIgnore] public MotelyItemSeal? SealEnum { get; set; }
        [JsonIgnore] public MotelyItemEnhancement? EnhancementEnum { get; set; }
        [JsonIgnore] public JokerWildcard? WildcardEnum { get; set; }
        [JsonIgnore] public bool IsWildcardAnyJoker { get; set; }
        
        public void InitializeParsedEnums()
        {
            // TODO: Copy the enum parsing logic from the old FilterItem class above
            // Parse Type, Value, Edition, etc. into the enum properties
        }
    }
    
        // Optional deck/stake settings
        public string Deck { get; set; } = "Red";
        public string Stake { get; set; } = "White";
        
        // Cached expensive calculations
        private int? _maxVoucherAnte;
        public int MaxVoucherAnte
        {
            get
            {
                if (_maxVoucherAnte.HasValue) return _maxVoucherAnte.Value;
                
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
                
                _maxVoucherAnte = maxAnte;
                return maxAnte;
            }
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
        public static bool TryLoadFromJsonFile(string jsonPath, [NotNullWhen(true)] out MotelyJsonConfig? config)
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

                var deserializedConfig = JsonSerializer.Deserialize<MotelyJsonConfig>(json, options);
                if (deserializedConfig == null)
                {
                    return false;
                }
                
                deserializedConfig.PostProcess();
                
                // Validate config
                MotelyJsonConfigValidator.ValidateConfig(deserializedConfig);
                
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
        /// Post-process after deserialization
        /// </summary>
        private void PostProcess()
        {
            // Process all filter items
            foreach (var item in Must.Concat(Should).Concat(MustNot))
            {
                // Normalize type
                item.Type = item.Type.ToLowerInvariant();
                
                // Normalize arrays - force empty arrays instead of null
                item.PackSlots ??= [];
                item.ShopSlots ??= [];
                item.Stickers ??= [];
                item.Antes ??= [1, 2, 3, 4, 5, 6, 7, 8]; // Default to all antes
                if (item.Sources?.PackSlots == null && item.Sources != null) 
                    item.Sources.PackSlots = [];
                if (item.Sources?.ShopSlots == null && item.Sources != null) 
                    item.Sources.ShopSlots = [];
                
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
            }

            // Second pass: compute per-item metadata (after overrides & masks) 
            foreach (var item in Must.Concat(Should).Concat(MustNot))
            {
                if (item.Sources?.PackSlots != null && item.Sources.PackSlots.Length > 0)
                    item.MaxPackSlot = item.Sources.PackSlots.Max();
                if (item.Sources?.ShopSlots != null && item.Sources.ShopSlots.Length > 0)
                    item.MaxShopSlot = item.Sources.ShopSlots.Max();

                item.IsWildcardAnyJoker = item.ItemTypeEnum == MotelyFilterItemType.Joker && item.WildcardEnum == JokerWildcard.AnyJoker;
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
                _ => new SourcesConfig
                {
                    ShopSlots = new[] { 0, 1, 2, 3 },
                    PackSlots = new[] { 0, 1, 2, 3 },
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