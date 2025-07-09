using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motely.Filters;

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
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Edition { get; set; } = string.Empty;
        public List<string> JokerStickers { get; set; } = new();
        public string Rank { get; set; } = string.Empty;
        public string Suit { get; set; } = string.Empty;
        public string Enchantment { get; set; } = string.Empty;
        public string Chip { get; set; } = string.Empty;
        public int DesireByAnte { get; set; } = 8;
        public int[] SearchAntes { get; set; } = Array.Empty<int>();
        public int Score { get; set; }

        // Strongly-typed enum properties for fast, validated config
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyJoker? JokerEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyPlanetCard? PlanetEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelySpectralCard? SpectralEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyItemTypeCategory? TypeEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyVoucher? VoucherEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyTag? TagEnum { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MotelyTarotCard? TarotEnum { get; set; }
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, GetOptions());
    }

    public static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public static OuijaConfig Load(string configName, JsonSerializerOptions options)
    {
        string[] attempts =
        [
            Path.Combine(AppContext.BaseDirectory, "ouija_configs", configName),
            Path.Combine(AppContext.BaseDirectory, "ouija_configs", configName + ".ouija.json"),
            Path.Combine(".", "ouija_configs", configName),
            Path.Combine(".", "ouija_configs", configName + ".ouija.json"),
            configName,
            configName + ".ouija.json"
        ];

        foreach (var path in attempts)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"Loading config from: {path}");
                string json = File.ReadAllText(path);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("filter_config", out var filterConfig))
                {
                    var config = filterConfig.Deserialize<OuijaConfig>(options);
                    if (config != null)
                    {
                        if (string.IsNullOrEmpty(config.Deck)) config.Deck = string.Empty;
                        if (string.IsNullOrEmpty(config.Stake)) config.Stake = string.Empty;
                        ValidateDesires(config);
                        return config;
                    }
                }
                else
                {
                    var config = doc.RootElement.Deserialize<OuijaConfig>(options);
                    if (config != null)
                    {
                        if (string.IsNullOrEmpty(config.Deck)) config.Deck = string.Empty;
                        if (string.IsNullOrEmpty(config.Stake)) config.Stake = string.Empty;
                        if (config.MaxSearchAnte > 8) config.MaxSearchAnte = 8;
                        ValidateDesires(config);
                        return config;
                    }
                }
            }
        }
        throw new FileNotFoundException($"Could not find Ouija config: {configName}");
    }

    // Validates that all required enum properties are present and valid in Needs and Wants
    private static void ValidateDesires(OuijaConfig config)
    {
        void ValidateDesire(OuijaConfig.Desire d, int idx, string listName)
        {
            string type = d.Type?.Trim() ?? string.Empty;
            // Add checks for each type that requires a specific enum
            switch (type)
            {
                case "SoulJoker":
                case "Joker":
                    if (d.JokerEnum == null)
                        throw new InvalidOperationException($"OuijaConfig error: {listName}[{idx}] (Type='{type}') is missing required JokerEnum property.");
                    break;
                case "Planet":
                    if (d.PlanetEnum == null)
                        throw new InvalidOperationException($"OuijaConfig error: {listName}[{idx}] (Type='{type}') is missing required PlanetEnum property.");
                    break;
                case "Spectral":
                    if (d.SpectralEnum == null)
                        throw new InvalidOperationException($"OuijaConfig error: {listName}[{idx}] (Type='{type}') is missing required SpectralEnum property.");
                    break;
                case "Voucher":
                    if (d.VoucherEnum == null)
                        throw new InvalidOperationException($"OuijaConfig error: {listName}[{idx}] (Type='{type}') is missing required VoucherEnum property.");
                    break;
                case "Tag":
                    if (d.TagEnum == null)
                        throw new InvalidOperationException($"OuijaConfig error: {listName}[{idx}] (Type='{type}') is missing required TagEnum property.");
                    break;
                case "Tarot":
                    if (d.TarotEnum == null)
                        throw new InvalidOperationException($"OuijaConfig error: {listName}[{idx}] (Type='{type}') is missing required TarotEnum property.");
                    break;
                case "Type":
                case "ItemType":
                    if (d.TypeEnum == null)
                        throw new InvalidOperationException($"OuijaConfig error: {listName}[{idx}] (Type='{type}') is missing required TypeEnum property.");
                    break;
                // Add more cases as needed for other types
            }
        }
        if (config.Needs != null)
        {
            for (int i = 0; i < config.Needs.Length; i++)
            {
                if (config.Needs[i] != null)
                    ValidateDesire(config.Needs[i], i, "Needs");
            }
        }
        if (config.Wants != null)
        {
            for (int i = 0; i < config.Wants.Length; i++)
            {
                if (config.Wants[i] != null)
                    ValidateDesire(config.Wants[i], i, "Wants");
            }
        }
    }
}
