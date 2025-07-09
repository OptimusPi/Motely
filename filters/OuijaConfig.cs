using System.Text.Json;
using System.Text.Json.Serialization;
using Motely;

namespace Motely.Filters;

/// <summary>
/// Unified Ouija configuration that supports both string-based (for JSON flexibility)
/// and strongly-typed (for performance) representations.
/// </summary>
public class OuijaConfig
{
    public int NumNeeds { get; set; }
    public int NumWants { get; set; }
    public Desire[] Needs { get; set; } = [];
    public Desire[] Wants { get; set; } = [];
    public int MaxSearchAnte { get; set; } = 8;
    public string Deck { get; set; } = string.Empty;
    public string Stake { get; set; } = string.Empty;
    public bool ScoreNaturalNegatives { get; set; }
    public bool ScoreDesiredNegatives { get; set; }

    public class Desire
    {
        // String-based properties for JSON compatibility
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Edition { get; set; } = string.Empty;
        
        // Direct enum properties for backward compatibility
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyJoker? JokerEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyPlanetCard? PlanetEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelySpectralCard? SpectralEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyVoucher? VoucherEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyTag? TagEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyTarotCard? TarotEnum { get; set; }
        
        // Additional string properties for complex filters
        public List<string> JokerStickers { get; set; } = new();
        public string Rank { get; set; } = string.Empty;
        public string Suit { get; set; } = string.Empty;
        public string Enchantment { get; set; } = string.Empty;
        public string Chip { get; set; } = string.Empty;
        
        // Scoring properties
        public int DesireByAnte { get; set; } = 8;
        public int[] SearchAntes { get; set; } = Array.Empty<int>();
        public int Score { get; set; } = 1;

        // Cached strongly-typed values (populated during validation)
        [JsonIgnore]
        public MotelyItemTypeCategory? TypeCategory { get; private set; }

        /// <summary>
        /// Validates and caches the strongly-typed enums from string values
        /// </summary>
        public void ValidateAndCache()
        {
            // If enums are already provided directly, we just need to ensure type is set
            if (JokerEnum.HasValue && string.IsNullOrEmpty(Type))
            {
                Type = "Joker";
                TypeCategory = MotelyItemTypeCategory.Joker;
                return;
            }
            if (PlanetEnum.HasValue && string.IsNullOrEmpty(Type))
            {
                Type = "PlanetCard";
                TypeCategory = MotelyItemTypeCategory.PlanetCard;
                return;
            }
            if (SpectralEnum.HasValue && string.IsNullOrEmpty(Type))
            {
                Type = "SpectralCard";
                TypeCategory = MotelyItemTypeCategory.SpectralCard;
                return;
            }
            if (TarotEnum.HasValue && string.IsNullOrEmpty(Type))
            {
                Type = "TarotCard";
                TypeCategory = MotelyItemTypeCategory.TarotCard;
                return;
            }
            if (TagEnum.HasValue && string.IsNullOrEmpty(Type))
            {
                Type = "Tag";
                return;
            }
            if (VoucherEnum.HasValue && string.IsNullOrEmpty(Type))
            {
                Type = "Voucher";
                return;
            }
            
            // Parse type category from string
            if (!string.IsNullOrEmpty(Type))
            {
                // Handle common aliases
                var normalizedType = Type;
                if (Type.Equals("Planet", StringComparison.OrdinalIgnoreCase))
                    normalizedType = "PlanetCard";
                else if (Type.Equals("Spectral", StringComparison.OrdinalIgnoreCase))
                    normalizedType = "SpectralCard";
                else if (Type.Equals("Tarot", StringComparison.OrdinalIgnoreCase))
                    normalizedType = "TarotCard";
                
                if (MotelyEnumUtil.TryParseEnum<MotelyItemTypeCategory>(normalizedType, out var cat))
                {
                    TypeCategory = cat;
                }
                else if (Type.Equals("Tag", StringComparison.OrdinalIgnoreCase))
                {
                    // Tag is a special case - not in MotelyItemTypeCategory
                }
                else if (Type.Equals("SoulJoker", StringComparison.OrdinalIgnoreCase))
                {
                    TypeCategory = MotelyItemTypeCategory.Joker; // Special joker type
                }
                else if (Type.Equals("Voucher", StringComparison.OrdinalIgnoreCase))
                {
                    // Voucher is also a special case
                }
            }

            // Parse value based on type (only if enum not already set)
            if (!string.IsNullOrEmpty(Value))
            {
                if ((TypeCategory == MotelyItemTypeCategory.Joker || Type.Equals("Joker", StringComparison.OrdinalIgnoreCase) || Type.Equals("SoulJoker", StringComparison.OrdinalIgnoreCase)) && !JokerEnum.HasValue)
                {
                    if (MotelyEnumUtil.TryParseEnum<MotelyJoker>(Value, out var joker))
                        JokerEnum = joker;
                }
                else if ((TypeCategory == MotelyItemTypeCategory.PlanetCard || Type.Equals("Planet", StringComparison.OrdinalIgnoreCase)) && !PlanetEnum.HasValue)
                {
                    if (MotelyEnumUtil.TryParseEnum<MotelyPlanetCard>(Value, out var planet))
                        PlanetEnum = planet;
                }
                else if ((TypeCategory == MotelyItemTypeCategory.SpectralCard || Type.Equals("Spectral", StringComparison.OrdinalIgnoreCase)) && !SpectralEnum.HasValue)
                {
                    if (MotelyEnumUtil.TryParseEnum<MotelySpectralCard>(Value, out var spectral))
                        SpectralEnum = spectral;
                }
                else if ((TypeCategory == MotelyItemTypeCategory.TarotCard || Type.Equals("Tarot", StringComparison.OrdinalIgnoreCase)) && !TarotEnum.HasValue)
                {
                    if (MotelyEnumUtil.TryParseEnum<MotelyTarotCard>(Value, out var tarot))
                        TarotEnum = tarot;
                }
                else if (Type.Equals("Tag", StringComparison.OrdinalIgnoreCase) && !TagEnum.HasValue)
                {
                    if (MotelyEnumUtil.TryParseEnum<MotelyTag>(Value, out var tag))
                        TagEnum = tag;
                }
                else if (Type.Equals("Voucher", StringComparison.OrdinalIgnoreCase) && !VoucherEnum.HasValue)
                {
                    if (MotelyEnumUtil.TryParseEnum<MotelyVoucher>(Value, out var voucher))
                        VoucherEnum = voucher;
                }
            }
        }

        /// <summary>
        /// Gets a display string for debugging
        /// </summary>
        public string GetDisplayString()
        {
            return Type switch
            {
                "Joker" or "SoulJoker" => JokerEnum?.ToString() ?? Value,
                "PlanetCard" or "Planet" => PlanetEnum?.ToString() ?? Value,
                "SpectralCard" or "Spectral" => SpectralEnum?.ToString() ?? Value,
                "TarotCard" or "Tarot" => TarotEnum?.ToString() ?? Value,
                "Tag" => TagEnum?.ToString() ?? Value,
                "Voucher" => VoucherEnum?.ToString() ?? Value,
                _ => Value
            };
        }
    }

    /// <summary>
    /// Validates the configuration and caches strongly-typed enums
    /// </summary>
    public void Validate()
    {
        // Validate deck and stake
        if (string.IsNullOrEmpty(Deck)) Deck = "RedDeck";
        if (string.IsNullOrEmpty(Stake)) Stake = "WhiteStake";
        if (MaxSearchAnte > 8) MaxSearchAnte = 8;

        // Validate and cache all desires
        if (Needs != null)
        {
            NumNeeds = Needs.Length;
            foreach (var need in Needs)
            {
                need?.ValidateAndCache();
                if (need != null && !IsDesireValid(need))
                {
                    throw new InvalidOperationException(
                        $"Need with Type='{need.Type}' and Value='{need.Value}' could not be parsed to valid enum");
                }
            }
        }

        if (Wants != null)
        {
            NumWants = Wants.Length;
            foreach (var want in Wants)
            {
                want?.ValidateAndCache();
                // Wants are optional, so we don't throw on invalid parsing
            }
        }
    }

    private bool IsDesireValid(Desire desire)
    {
        // For needs, we require valid enum parsing
        return desire.Type switch
        {
            "Joker" => desire.JokerEnum.HasValue,
            "SoulJoker" => desire.JokerEnum.HasValue,
            "Planet" or "PlanetCard" => desire.PlanetEnum.HasValue,
            "Spectral" or "SpectralCard" => desire.SpectralEnum.HasValue,
            "Tarot" or "TarotCard" => desire.TarotEnum.HasValue,
            "Tag" => desire.TagEnum.HasValue,
            "Voucher" => desire.VoucherEnum.HasValue,
            _ => false
        };
    }

    /// <summary>
    /// Creates a user-friendly JSON representation
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Loads configuration from JSON file with validation
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

        OuijaConfig? config = null;
        
        // Try to parse directly first
        try
        {
            config = JsonSerializer.Deserialize<OuijaConfig>(json, options);
            Console.WriteLine($"[DEBUG] Direct parse: Found {config?.Needs?.Length ?? 0} needs");
            if (config != null && (config.Needs == null || config.Needs.Length == 0))
            {
                // Direct parse didn't work, force wrapper parse
                throw new Exception("Direct parse returned empty config, trying wrapper");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Direct parse failed: {ex.Message}");
            // Try parsing from a wrapper object
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("filter_config", out var filterConfig))
            {
                config = filterConfig.Deserialize<OuijaConfig>(options);
                Console.WriteLine($"[DEBUG] Wrapper parse: Found {config?.Needs?.Length ?? 0} needs");
            }
            else
            {
                Console.WriteLine("[DEBUG] No filter_config property found");
                // List all root properties for debugging
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    Console.WriteLine($"[DEBUG] Root property: {prop.Name}");
                }
            }
        }

        if (config == null)
            throw new InvalidOperationException("Failed to parse config file");

        Console.WriteLine($"[DEBUG] Before validate: {config.Needs?.Length ?? 0} needs, {config.Wants?.Length ?? 0} wants");
        config.Validate();
        Console.WriteLine($"[DEBUG] After validate: {config.NumNeeds} needs, {config.NumWants} wants");
        return config;
    }

    /// <summary>
    /// Creates an example configuration for reference
    /// </summary>
    public static OuijaConfig CreateExample()
    {
        return new OuijaConfig
        {
            MaxSearchAnte = 8,
            Deck = "RedDeck",
            Stake = "WhiteStake",
            ScoreNaturalNegatives = true,
            ScoreDesiredNegatives = false,
            Needs = new[]
            {
                new Desire
                {
                    Type = "Joker",
                    Value = "Perkeo",
                    DesireByAnte = 2,
                    Score = 10
                },
                new Desire
                {
                    Type = "Tag",
                    Value = "NegativeTag",
                    DesireByAnte = 3,
                    Score = 5
                }
            },
            Wants = new[]
            {
                new Desire
                {
                    Type = "Joker",
                    Value = "Blueprint",
                    Edition = "Negative",
                    DesireByAnte = 4,
                    Score = 3
                },
                new Desire
                {
                    Type = "Planet",
                    Value = "Pluto",
                    SearchAntes = new[] { 1, 2, 3 },
                    Score = 2
                }
            }
        };
    }
}
