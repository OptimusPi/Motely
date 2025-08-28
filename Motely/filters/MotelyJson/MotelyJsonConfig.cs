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
    public enum MotelyJsonConfigWildcards
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
        public string? Label { get; set; }
        public int[] Antes { get; set; } = [];
        
        public int Score { get; set; } = 1;
        public int? Min { get; set; }
        public int? FilterOrder { get; set; }  // Optional ordering for slice chain optimization
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
            get => Antes;
            set
            {
                Antes = value;
            }
        }

        // Pre-computed values (set during initialization)

        public int? MaxShopSlot { get; set; }
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
            // Parse ItemTypeEnum from Type string
            ItemTypeEnum = Type?.ToLowerInvariant() switch
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
            
            // Parse Value based on ItemType
            if (!string.IsNullOrEmpty(Value))
            {
                // Check for wildcards first
                if (Value.Equals("Any", StringComparison.OrdinalIgnoreCase))
                {
                    WildcardEnum = MotelyJsonConfigWildcards.AnyJoker; // Generic "Any" wildcard
                    IsWildcard = true;
                }
                else if (Value.Equals("AnyCommon", StringComparison.OrdinalIgnoreCase))
                {
                    WildcardEnum = MotelyJsonConfigWildcards.AnyCommon;
                    IsWildcard = true;
                }
                else if (Value.Equals("AnyUncommon", StringComparison.OrdinalIgnoreCase))
                {
                    WildcardEnum = MotelyJsonConfigWildcards.AnyUncommon;
                    IsWildcard = true;
                }
                else if (Value.Equals("AnyRare", StringComparison.OrdinalIgnoreCase))
                {
                    WildcardEnum = MotelyJsonConfigWildcards.AnyRare;
                    IsWildcard = true;
                }
                else if (Value.Equals("AnyLegendary", StringComparison.OrdinalIgnoreCase))
                {
                    WildcardEnum = MotelyJsonConfigWildcards.AnyLegendary;
                    IsWildcard = true;
                }
                else
                {
                    // Parse specific enum values based on type
                    switch (ItemTypeEnum)
                    {
                        case MotelyFilterItemType.Joker:
                        case MotelyFilterItemType.SoulJoker:
                            if (Enum.TryParse<MotelyJoker>(Value, true, out var joker))
                                JokerEnum = joker;
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
                            TagTypeEnum = ItemTypeEnum == MotelyFilterItemType.SmallBlindTag 
                                ? MotelyTagType.SmallBlind 
                                : MotelyTagType.BigBlind;
                            break;
                    }
                }
            }
            
            // Parse Edition
            if (!string.IsNullOrEmpty(Edition))
            {
                if (Enum.TryParse<MotelyItemEdition>(Edition, true, out var edition))
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
            
            // Parse PlayingCard specific properties
            if (ItemTypeEnum == MotelyFilterItemType.PlayingCard)
            {
                if (!string.IsNullOrEmpty(Suit) && Enum.TryParse<MotelyPlayingCardSuit>(Suit, true, out var suit))
                    SuitEnum = suit;
                
                if (!string.IsNullOrEmpty(Rank) && Enum.TryParse<MotelyPlayingCardRank>(Rank, true, out var rank))
                    RankEnum = rank;
                
                if (!string.IsNullOrEmpty(Seal) && Enum.TryParse<MotelyItemSeal>(Seal, true, out var seal))
                    SealEnum = seal;
                
                if (!string.IsNullOrEmpty(Enhancement) && Enum.TryParse<MotelyItemEnhancement>(Enhancement, true, out var enhancement))
                    EnhancementEnum = enhancement;
            }
        }
    }
    
        // Optional deck/stake settings
        public string Deck { get; set; } = "Red";
        public string Stake { get; set; } = "White";
        
        // Pre-computed expensive calculations (set during PostProcess, immutable after)
        [JsonIgnore]
        public int MaxVoucherAnte { get; private set; }

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
                    AllowTrailingCommas = true
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
                error = $"JSON syntax error at line {jex.LineNumber}, position {jex.BytePositionInLine}: {jex.Message}";
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
        /// Post-process after deserialization
        /// </summary>
        private void PostProcess()
        {
            // Process all filter items
            foreach (var item in Must.Concat(Should).Concat(MustNot))
            {
                // Normalize type
                item.Type = item.Type.ToLowerInvariant();
                
                // Normalize arrays - but DON'T initialize flat pack/shop slots as that breaks Sources merging
                // item.PackSlots and item.ShopSlots should remain null if not provided
                item.Stickers ??= [];
                item.Antes ??= [1, 2, 3, 4, 5, 6, 7, 8]; // Default to all antes
                // Don't overwrite Sources arrays if they already exist from JSON
                if (item.Sources != null) 
                {
                    item.Sources.PackSlots ??= [];
                    item.Sources.ShopSlots ??= [];
                }
                
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
                    if (item.PackSlots != null && item.PackSlots.Length > 0)
                    {
                        // Debug check: warn if overwriting non-empty Sources
                        Debug.Assert(item.Sources.PackSlots == null || item.Sources.PackSlots.Length == 0, 
                            "Warning: Overwriting non-empty Sources.PackSlots with flat PackSlots property");
                        item.Sources.PackSlots = item.PackSlots;
                    }
                    if (item.ShopSlots != null && item.ShopSlots.Length > 0)
                    {
                        // Debug check: warn if overwriting non-empty Sources
                        Debug.Assert(item.Sources.ShopSlots == null || item.Sources.ShopSlots.Length == 0, 
                            "Warning: Overwriting non-empty Sources.ShopSlots with flat ShopSlots property");
                        item.Sources.ShopSlots = item.ShopSlots;
                    }
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

                item.IsWildcard = item.ItemTypeEnum == MotelyFilterItemType.Joker && item.WildcardEnum == MotelyJsonConfigWildcards.AnyJoker;
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