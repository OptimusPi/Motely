using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace Motely.Filters;

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
                IncludeShopStream = Sources.Contains("shop");
                IncludeBoosterPacks = Sources.Contains("packs");
                IncludeSkipTags = Sources.Contains("tags");
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