using System;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    [JsonPropertyName("must")]
    public List<FilterItem> Must { get; set; } = new();
    
    [JsonPropertyName("should")]
    public List<FilterItem> Should { get; set; } = new();
    
    [JsonPropertyName("mustNot")]
    public List<FilterItem> MustNot { get; set; } = new();
    
    [JsonPropertyName("minimumScore")]
    public int MinimumScore { get; set; } = 0;
    
    // Optional deck/stake settings
    [JsonPropertyName("deck")]
    public string Deck { get; set; } = "Red";
    
    [JsonPropertyName("stake")]
    public string Stake { get; set; } = "White";
    
    [JsonPropertyName("maxSearchAnte")]
    public int MaxSearchAnte { get; set; } = 39; // naneInf score to win so this is technically Max Ante in Balatro! Neat! 🃏
    
    // Support nested filter format
    [JsonPropertyName("filter")]
    public FilterSettings? Filter { get; set; }
    
    public class FilterSettings
    {
        [JsonPropertyName("deck")]
        public string? Deck { get; set; }
        
        [JsonPropertyName("stake")]
        public string? Stake { get; set; }
        
        [JsonPropertyName("maxAnte")]
        public int? MaxAnte { get; set; }
    }
    
    public class FilterItem
    {
        // Support both formats
        [JsonPropertyName("Type")]
        public string Type { get; set; } = "";
        
        [JsonPropertyName("Value")]
        public string Value { get; set; } = "";
        
        // Nested item format
        [JsonPropertyName("item")]
        public ItemInfo? Item { get; set; }
        
        [JsonPropertyName("SearchAntes")]
        public int[] SearchAntes { get; set; } = { 1, 2, 3, 4, 5, 6, 7, 8 };
        
        [JsonPropertyName("antes")]
        public int[]? Antes { get; set; }
        
        [JsonPropertyName("Score")]
        public int Score { get; set; } = 1;
        
        [JsonPropertyName("Edition")]
        public string? Edition { get; set; }
        
        [JsonPropertyName("Stickers")]
        public List<string>? Stickers { get; set; }
        
        // PlayingCard specific
        [JsonPropertyName("Suit")]
        public string? Suit { get; set; }
        
        [JsonPropertyName("Rank")]
        public string? Rank { get; set; }
        
        [JsonPropertyName("Seal")]
        public string? Seal { get; set; }
        
        [JsonPropertyName("Enhancement")]
        public string? Enhancement { get; set; }
        
        // Sources
        [JsonPropertyName("IncludeShopStream")]
        public bool IncludeShopStream { get; set; } = true;
        
        [JsonPropertyName("IncludeBoosterPacks")]
        public bool IncludeBoosterPacks { get; set; } = true;
        
        [JsonPropertyName("IncludeSkipTags")]
        public bool IncludeSkipTags { get; set; } = true;
        
        [JsonPropertyName("sources")]
        public List<string>? Sources { get; set; }
        
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
            }
            
            if (Antes != null)
            {
                SearchAntes = Antes;
            }
            
            if (Sources != null)
            {
                // Validate sources
                foreach (var source in Sources)
                {
                    if (!ValidItemSources.IsValid(source))
                    {
                        var validSources = string.Join(", ", ValidItemSources.AllSources.Select(s => $"'{s}'"));
                        throw new ArgumentException($"Invalid source '{source}'. Valid sources are: {validSources}");
                    }
                }
                
                // Process sources (case-insensitive)
                var lowerSources = Sources.Select(s => s.ToLowerInvariant()).ToList();
                IncludeShopStream = lowerSources.Contains(ValidItemSources.Shop);
                IncludeBoosterPacks = lowerSources.Contains(ValidItemSources.Packs);
                IncludeSkipTags = lowerSources.Contains(ValidItemSources.Tags);
            }
            
            // === PARSE STRINGS TO ENUMS FOR PERFORMANCE ===
            ParseEnums();
        }
        
        private void ParseEnums()
        {
            var typeLower = Type.ToLower();
            
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
                    
                case "tag":
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
                    throw new ArgumentException($"Invalid item type: '{Type}'. Valid types are: joker, souljoker, tarot, tarotcard, spectral, spectralcard, planet, planetcard, tag, voucher, playingcard");
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
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("edition")]
        public string? Edition { get; set; }
        
        // Playing card specific
        [JsonPropertyName("rank")]
        public string? Rank { get; set; }
        
        [JsonPropertyName("suit")]
        public string? Suit { get; set; }
        
        [JsonPropertyName("enhancement")]
        public string? Enhancement { get; set; }
        
        [JsonPropertyName("seal")]
        public string? Seal { get; set; }
        
        [JsonPropertyName("value")]
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
            AllowTrailingCommas = true
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
            if (Filter.MaxAnte.HasValue)
                MaxSearchAnte = Filter.MaxAnte.Value;
        }
        
        // Basic validation
        if (MaxSearchAnte > 39) MaxSearchAnte = 39;
        if (MaxSearchAnte < 1) MaxSearchAnte = 1;
        
        // Initialize and validate all filter items
        foreach (var item in Must.Concat(Should).Concat(MustNot))
        {
            // Initialize from nested format
            item.Initialize();
            
            // Ensure all SearchAntes arrays are within bounds
            item.SearchAntes = item.SearchAntes.Where(a => a >= 1 && a <= MaxSearchAnte).ToArray();
            if (item.SearchAntes.Length == 0)
                item.SearchAntes = new[] { 1 };
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