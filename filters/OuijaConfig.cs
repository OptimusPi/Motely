using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Motely.Filters;

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
        
        [JsonPropertyName("searchAntes")]
        public int[]? SearchAntes { get; set; }
        
        [JsonPropertyName("antes")]
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
        
        // Get effective antes (searchAntes takes precedence over antes)
        [JsonIgnore]
        public int[] EffectiveAntes => SearchAntes ?? Antes ?? new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        
        // === COMPUTED ENUM PROPERTIES FOR COMPATIBILITY ===
        // These parse on-demand from the string values
        
        [JsonIgnore]
        public MotelyFilterItemType? ItemTypeEnum => Type?.ToLowerInvariant() switch
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
            "boss" or "bossblind" => MotelyFilterItemType.Boss,
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
            !string.IsNullOrEmpty(Rank) && Enum.TryParse<MotelyPlayingCardRank>(NormalizeRank(Rank), true, out var rank) ? rank : null;
        
        [JsonIgnore]
        public MotelyPlayingCardSuit? SuitEnum => 
            !string.IsNullOrEmpty(Suit) && Enum.TryParse<MotelyPlayingCardSuit>(Suit, true, out var suit) ? suit : null;
        
        [JsonIgnore]
        public MotelyItemEnhancement? EnhancementEnum => 
            !string.IsNullOrEmpty(Enhancement) && Enum.TryParse<MotelyItemEnhancement>(Enhancement, true, out var enh) ? enh : null;
        
        [JsonIgnore]
        public MotelyItemSeal? SealEnum => 
            !string.IsNullOrEmpty(Seal) && Enum.TryParse<MotelyItemSeal>(Seal, true, out var seal) ? seal : null;
        
        [JsonIgnore]
        public List<MotelyJokerSticker>? StickerEnums
        {
            get
            {
                if (Stickers == null || Stickers.Count == 0) return null;
                var result = new List<MotelyJokerSticker>();
                foreach (var sticker in Stickers)
                {
                    if (Enum.TryParse<MotelyJokerSticker>(sticker, true, out var s))
                        result.Add(s);
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
        
        private static string NormalizeRank(string rank)
        {
            return rank?.ToLowerInvariant() switch
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
                _ => rank ?? ""
            };
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
    /// Load from JSON file
    /// </summary>
    public static OuijaConfig LoadFromJson(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException($"Config file not found: {jsonPath}");

        var json = File.ReadAllText(jsonPath);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new SourcesConfigConverter() }
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
                Deck = Filter.Deck;
            if (!string.IsNullOrEmpty(Filter.Stake))
                Stake = Filter.Stake;
        }

        // Process all filter items
        foreach (var item in Must.Concat(Should).Concat(MustNot))
        {
            // Normalize type
            item.Type = item.Type.ToLowerInvariant();
            
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

/// <summary>
/// Custom converter to handle both legacy array format and new object format for sources
/// </summary>
public class SourcesConfigConverter : JsonConverter<OuijaConfig.SourcesConfig?>
{
    public override OuijaConfig.SourcesConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
            
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // Legacy format: array of strings like ["shop", "packs", "tags"]
            var sources = new OuijaConfig.SourcesConfig();
            var legacyList = JsonSerializer.Deserialize<List<string>>(ref reader, options);
            
            if (legacyList != null)
            {
                foreach (var source in legacyList.Select(s => s.ToLowerInvariant()))
                {
                    switch (source)
                    {
                        case "shop":
                            sources.ShopSlots = new[] { 0, 1, 2, 3, 4, 5 };
                            break;
                        case "packs":
                            sources.PackSlots = new[] { 0, 1, 2, 3, 4, 5 };
                            break;
                        case "tags":
                            sources.Tags = true;
                            break;
                    }
                }
            }
            
            return sources;
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            // New format: object with specific slots
            // Create new options without this converter to avoid recursion
            var newOptions = new JsonSerializerOptions(options);
            newOptions.Converters.Remove(this);
            return JsonSerializer.Deserialize<OuijaConfig.SourcesConfig>(ref reader, newOptions);
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