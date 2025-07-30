using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace Motely.Filters;

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
        
        // Initialize from nested format if present
        public void Initialize()
        {
            if (Item != null)
            {
                Type = Item.Type ?? "";
                Value = Item.Name ?? "";
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
    }
    
    /// <summary>
    /// Load from JSON - MongoDB-style must/should/mustNot format
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