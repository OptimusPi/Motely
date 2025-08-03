using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Motely.Filters;

// REMOVED FilterItemType enum - use Motely enums directly!

/// <summary>
/// Valid item sources for filtering
/// </summary>
public static class ValidItemSources
{
    public const string Shop = "shop";   // Items in the shop
    public const string Packs = "packs"; // Items from booster packs
    public const string Tags = "tags";   // Items from skip tags
    
    public static readonly HashSet<string> AllSources = new()
    {
        Shop, Packs, Tags
    };
    
    public static bool IsValid(string source) => 
        AllSources.Contains(source?.ToLowerInvariant() ?? "");
}

/// <summary>
/// MongoDB compound Operator-style Ouija configuration
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
    
    public class SourcesConfig
    {
        public int[]? ShopSlots { get; set; }
        public int[]? PackSlots { get; set; }
        public bool? Tags { get; set; }
        public bool? RequireMega { get; set; }
        
        // Legacy support - if sources is just an array of strings
        public static implicit operator SourcesConfig?(List<string>? legacy)
        {
            if (legacy == null) return null;
            
            var config = new SourcesConfig();
            var lowerSources = legacy.Select(s => s.ToLowerInvariant()).ToList();
            
            // Convert legacy format
            if (lowerSources.Contains("shop"))
            {
                // Shop means all shop slots
                config.ShopSlots = Enumerable.Range(0, 6).ToArray();
            }
            
            if (lowerSources.Contains("packs"))
            {
                // Packs means all pack slots
                config.PackSlots = Enumerable.Range(0, 6).ToArray();
            }
            
            if (lowerSources.Contains("tags"))
            {
                config.Tags = true;
            }
            
            return config;
        }
    }
    
    public class FilterItem
    {
        // Support both formats
        public string Type { get; set; } = "";
        public string Value { get; set; } = "";
        
        // Nested item format
        public ItemInfo? Item { get; set; }
        
        // Support both SearchAntes and antes
        private int[]? _searchAntes;
        private int[]? _antes;
        
        public int[] SearchAntes 
        { 
            get => _searchAntes ?? _antes ?? new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            set => _searchAntes = value;
        }
        
        public int[]? Antes 
        { 
            get => _antes ?? _searchAntes;
            set => _antes = value;
        }
        
        public int Score { get; set; } = 1;
        public string? Edition { get; set; }
        public List<string>? Stickers { get; set; }
        
        // PlayingCard specific
        public string? Suit { get; set; }
        public string? Rank { get; set; }
        public string? Seal { get; set; }
        public string? Enhancement { get; set; }
        
        // Sources configuration
        [JsonConverter(typeof(SourcesConverter))]
        public SourcesConfig? Sources { get; set; }
        
        // === PARSED ENUM VALUES FOR PERFORMANCE ===
        // These are populated during Initialize() to avoid string comparisons in hot path
        
        [JsonIgnore]
        public MotelyJoker? JokerEnum { get; set; }
        
        [JsonIgnore]
        public MotelyTarotCard? TarotEnum { get; set; }
        
        [JsonIgnore]
        public MotelySpectralCard? SpectralEnum { get; set; }
        
        [JsonIgnore]
        public MotelyPlanetCard? PlanetEnum { get; set; }
        
        [JsonIgnore]
        public MotelyTag? TagEnum { get; set; }
        
        [JsonIgnore]
        public MotelyFilterItemType? ItemTypeEnum { get; set; }
        
        [JsonIgnore]
        public MotelyVoucher? VoucherEnum { get; set; }
        
        [JsonIgnore]
        public MotelyItemEdition? EditionEnum { get; set; }
        
        [JsonIgnore]
        public MotelyPlayingCardRank? RankEnum { get; set; }
        
        [JsonIgnore]
        public MotelyPlayingCardSuit? SuitEnum { get; set; }
        
        [JsonIgnore]
        public MotelyItemEnhancement? EnhancementEnum { get; set; }
        
        [JsonIgnore]
        public MotelyItemSeal? SealEnum { get; set; }
        
        [JsonIgnore]
        public List<MotelyJokerSticker>? StickerEnums { get; set; }
        
        [JsonIgnore]
        public MotelyTagType? TagTypeEnum { get; set; }
        
        // Initialize from nested format if present
        public void Initialize()
        {
            if (Item != null)
            {
                Type = Item.Type ?? "";
                
                // Handle different item types
                if (Type.ToLower() == "playingcard")
                {
                    // For playing cards, use rank/suit from nested item
                    if (!string.IsNullOrEmpty(Item.Rank))
                        Rank = Item.Rank;
                    if (!string.IsNullOrEmpty(Item.Value)) // Support "value" as alternative to "rank"
                        Rank = Item.Value;
                    if (!string.IsNullOrEmpty(Item.Suit))
                        Suit = Item.Suit;
                    if (!string.IsNullOrEmpty(Item.Enhancement))
                        Enhancement = Item.Enhancement;
                    if (!string.IsNullOrEmpty(Item.Seal))
                        Seal = Item.Seal;
                }
                else
                {
                    // For other types (jokers, etc), use name
                    Value = Item.Name ?? Item.Value ?? "";
                }
                
                if (!string.IsNullOrEmpty(Item.Edition))
                    Edition = Item.Edition;
                if (Item.Stickers != null)
                    Stickers = Item.Stickers;
            }
            
            if (Antes != null)
            {
                SearchAntes = Antes;
            }
            
            // Parse the item type early to determine default sources
            var typeLower = Type.ToLower();
            ItemTypeEnum = typeLower switch
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
                _ => null
            };
            
            // Handle sources format
            if (Sources != null)
            {
                // Validate the slot indices
                if (Sources.ShopSlots != null)
                {
                    foreach (var slot in Sources.ShopSlots)
                    {
                        if (slot < 0 || slot > 999)
                            throw new ArgumentException($"Invalid shop slot {slot}. Valid slots are 1-999.");
                    }
                }
                
                if (Sources.PackSlots != null)
                {
                    foreach (var slot in Sources.PackSlots)
                    {
                        if (slot < 0 || slot > 5)
                            throw new ArgumentException($"Invalid pack slot {slot}. Valid slots are 0-5.");
                    }
                }
            }
            else
            {
                // Default sources if not specified - includes all valid sources for the item type
                Sources = new SourcesConfig
                {
                    ShopSlots = new[] { 0, 1, 2, 3, 4, 5 }, // All shop slots (ante 1 has 4, others have 6)
                    PackSlots = new[] { 0, 1, 2, 3, 4, 5 }, // All pack slots (ante 1 has 2, others have 6)
                    Tags = true
                };
                
                // Legendary jokers (SoulJokers) can't appear in shops
                if (ItemTypeEnum == MotelyFilterItemType.SoulJoker)
                {
                    Sources.ShopSlots = new int[] { };
                }
            }
            
            // === PARSE REMAINING ENUMS FOR PERFORMANCE ===
            ParseEnums();
        }
        
        private void ParseEnums()
        {
            var typeLower = Type.ToLower();
            
            // ItemTypeEnum already parsed in Initialize()
            
            // Parse based on type - directly to Motely enums
            switch (typeLower)
            {
                case "joker":
                case "souljoker":
                    if (!string.IsNullOrEmpty(Value) && 
                        !Value.Equals("any", StringComparison.OrdinalIgnoreCase) && 
                        !Value.Equals("*", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Enum.TryParse<MotelyJoker>(Value, true, out var joker))
                        {
                            throw new ArgumentException($"Invalid joker value: '{Value}'. Must be a valid MotelyJoker enum value.");
                        }
                        JokerEnum = joker;
                    }
                    break;
                    
                case "tarot":
                case "tarotcard":
                    if (!string.IsNullOrEmpty(Value))
                    {
                        if (!Enum.TryParse<MotelyTarotCard>(Value, true, out var tarot))
                        {
                            throw new ArgumentException($"Invalid tarot value: '{Value}'. Must be a valid MotelyTarotCard enum value.");
                        }
                        TarotEnum = tarot;
                    }
                    break;
                    
                case "spectral":
                case "spectralcard":
                    if (!string.IsNullOrEmpty(Value) && 
                        !Value.Equals("any", StringComparison.OrdinalIgnoreCase) && 
                        !Value.Equals("*", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Enum.TryParse<MotelySpectralCard>(Value, true, out var spectral))
                        {
                            throw new ArgumentException($"Invalid spectral value: '{Value}'. Must be a valid MotelySpectralCard enum value.");
                        }
                        SpectralEnum = spectral;
                    }
                    break;
                    
                case "planet":
                case "planetcard":
                    if (!string.IsNullOrEmpty(Value))
                    {
                        if (!Enum.TryParse<MotelyPlanetCard>(Value, true, out var planet))
                        {
                            throw new ArgumentException($"Invalid planet value: '{Value}'. Must be a valid MotelyPlanetCard enum value.");
                        }
                        PlanetEnum = planet;
                    }
                    break;
                    
                case "smallblindtag":
                case "bigblindtag":
                    if (!string.IsNullOrEmpty(Value))
                    {
                        if (!Enum.TryParse<MotelyTag>(Value, true, out var tag))
                        {
                            throw new ArgumentException($"Invalid tag value: '{Value}'. Must be a valid MotelyTag enum value.");
                        }
                        TagEnum = tag;
                    }
                    
                    // Set the tag type enum based on the type string
                    TagTypeEnum = typeLower switch
                    {
                        "smallblindtag" => MotelyTagType.SmallBlind,
                        "bigblindtag" => MotelyTagType.BigBlind,
                        _ => MotelyTagType.Any
                    };
                    break;
                    
                case "voucher":
                    if (!string.IsNullOrEmpty(Value))
                    {
                        if (!Enum.TryParse<MotelyVoucher>(Value, true, out var voucher))
                        {
                            throw new ArgumentException($"Invalid voucher value: '{Value}'. Must be a valid MotelyVoucher enum value.");
                        }
                        VoucherEnum = voucher;
                    }
                    break;
                    
                case "playingcard":
                    // Parse rank with normalization
                    if (!string.IsNullOrEmpty(Rank))
                    {
                        var normalizedRank = NormalizeRank(Rank);
                        if (!Enum.TryParse<MotelyPlayingCardRank>(normalizedRank, true, out var rank))
                        {
                            throw new ArgumentException($"Invalid playing card rank: '{Rank}'.");
                        }
                        RankEnum = rank;
                    }
                    
                    // Parse suit
                    if (!string.IsNullOrEmpty(Suit))
                    {
                        if (!Enum.TryParse<MotelyPlayingCardSuit>(Suit, true, out var suit))
                        {
                            throw new ArgumentException($"Invalid playing card suit: '{Suit}'. Must be Hearts, Diamonds, Clubs, or Spades.");
                        }
                        SuitEnum = suit;
                    }
                    
                    // Parse enhancement
                    if (!string.IsNullOrEmpty(Enhancement) && 
                        !Enhancement.Equals("any", StringComparison.OrdinalIgnoreCase) && 
                        !Enhancement.Equals("*", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Enum.TryParse<MotelyItemEnhancement>(Enhancement, true, out var enhancement))
                        {
                            throw new ArgumentException($"Invalid enhancement: '{Enhancement}'. Must be a valid MotelyItemEnhancement enum value.");
                        }
                        EnhancementEnum = enhancement;
                    }
                    
                    // Parse seal
                    if (!string.IsNullOrEmpty(Seal) && 
                        !Seal.Equals("any", StringComparison.OrdinalIgnoreCase) && 
                        !Seal.Equals("*", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Enum.TryParse<MotelyItemSeal>(Seal, true, out var seal))
                        {
                            throw new ArgumentException($"Invalid seal: '{Seal}'. Must be a valid MotelyItemSeal enum value.");
                        }
                        SealEnum = seal;
                    }
                    break;
                    
                default:
                    throw new ArgumentException($"Invalid item type: '{Type}'. Valid types are: joker, souljoker, tarot, tarotcard, spectral, spectralcard, planet, planetcard, smallblindtag, bigblindtag, voucher, playingcard");
            }
            
            // Parse edition (common to all item types)
            if (!string.IsNullOrEmpty(Edition) && 
                !Edition.Equals("any", StringComparison.OrdinalIgnoreCase) && 
                !Edition.Equals("*", StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse<MotelyItemEdition>(Edition, true, out var edition))
                {
                    throw new ArgumentException($"Invalid edition: '{Edition}'. Must be a valid MotelyItemEdition enum value.");
                }
                EditionEnum = edition;
            }
            
            // Parse stickers (only applies to jokers)
            if (typeLower == "joker" || typeLower == "souljoker")
            {
                if (Stickers != null && Stickers.Count > 0)
                {
                    StickerEnums = new List<MotelyJokerSticker>();
                    foreach (var sticker in Stickers)
                    {
                        if (!string.IsNullOrEmpty(sticker))
                        {
                            if (!Enum.TryParse<MotelyJokerSticker>(sticker, true, out var stickerEnum))
                            {
                                throw new ArgumentException($"Invalid sticker: '{sticker}'. Must be one of: None, Eternal, Perishable, Rental");
                            }
                            StickerEnums.Add(stickerEnum);
                        }
                    }
                }
            }
        }
        
        private static string NormalizeRank(string rank)
        {
            return rank?.ToLower() switch
            {
                "2" or "two" => "Two",
                "3" or "three" => "Three",
                "4" or "four" => "Four",
                "5" or "five" => "Five",
                "6" or "six" => "Six",
                "7" or "seven" => "Seven",
                "8" or "eight" => "Eight",
                "9" or "nine" => "Nine",
                "10" or "ten" => "Ten",
                "j" or "jack" => "Jack",
                "q" or "queen" => "Queen",
                "k" or "king" => "King",
                "a" or "ace" => "Ace",
                _ => throw new ArgumentException($"Invalid rank: '{rank}'. Valid ranks are: 2-10, J, Q, K, A (or spelled out: Two-Ten, Jack, Queen, King, Ace)")
            };
        }
    }
    
    public class ItemInfo
    {
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? Edition { get; set; }
        public List<string>? Stickers { get; set; }
        
        // Playing card specific
        public string? Rank { get; set; }
        public string? Suit { get; set; }
        public string? Enhancement { get; set; }
        public string? Seal { get; set; }
        public string? Value { get; set; } // Alternative to name for backward compatibility
    }
    
    /// <summary>
    /// Load from JSON - MongoDB-style must/should/mustNot format
    /// </summary>
    public static OuijaConfig LoadFromJson(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Config file not found: {jsonPath}");

        var json = File.ReadAllText(jsonPath);
        
        // Validate JSON structure first
        StrictJsonValidator.ValidateJson(json, jsonPath);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = false
        };

        var config = JsonSerializer.Deserialize<OuijaConfig>(json, options) 
            ?? throw new InvalidOperationException("Failed to parse config");
            
        config.Validate();
        return config;
    }
    
    /// <summary>
    /// Validate the configuration
    /// </summary>
    public void Validate()
    {
        // Apply nested filter settings if present
        if (Filter != null)
        {
            if (!string.IsNullOrEmpty(Filter.Deck))
                Deck = Filter.Deck;
            if (!string.IsNullOrEmpty(Filter.Stake))
                Stake = Filter.Stake;
        }

        // Initialize and validate all filter items
        foreach (var item in Must.Concat(Should).Concat(MustNot))
        {
            // Initialize from nested format
            item.Initialize();

            // Ensure all SearchAntes arrays are within bounds
            item.SearchAntes = item.SearchAntes.ToArray();
            if (item.SearchAntes.Length == 0)
                item.SearchAntes = new[] { 1 };
                
            // Validate game logic constraints
            if (item.JokerEnum.HasValue && item.Sources?.ShopSlots != null && item.Sources.ShopSlots.Length > 0)
            {
                var legendaryJokers = new[] { 
                    MotelyJoker.Perkeo, 
                    MotelyJoker.Canio, 
                    MotelyJoker.Triboulet, 
                    MotelyJoker.Yorick, 
                    MotelyJoker.Chicot 
                };
                
                if (legendaryJokers.Contains(item.JokerEnum.Value))
                {
                    throw new InvalidOperationException($"Invalid config: {item.JokerEnum.Value} is a legendary joker and cannot appear in shops. Remove shop from sources or use a different joker.");
                }
            }
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
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }
}

// Custom JSON converter to handle both legacy array format and new object format for sources
public class SourcesConverter : JsonConverter<OuijaConfig.SourcesConfig?>
{
    public override OuijaConfig.SourcesConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
            
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Legacy format: array of strings
            var legacyList = JsonSerializer.Deserialize<List<string>>(ref reader, options);
            return legacyList; // Use implicit conversion
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            // New format: object with shopSlots, packSlots, tags
            return JsonSerializer.Deserialize<OuijaConfig.SourcesConfig>(ref reader, options);
        }
        else
        {
            throw new JsonException($"Unexpected token type for sources: {reader.TokenType}");
        }
    }
    
    public override void Write(Utf8JsonWriter writer, OuijaConfig.SourcesConfig? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        
        // Always write in new format
        JsonSerializer.Serialize(writer, value, options);
    }
}