using System.Text.Json;

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
    }

    public override string ToString()
    {
        static string FormatDesire(Desire d) {
            if (d.Type == "Joker" || d.Type == "SoulJoker")
                return $"Type='{d.Type}', Value='{d.Value}', Edition='{d.Edition}', JokerStickers=[{string.Join(",", d.JokerStickers)}], DesireByAnte={d.DesireByAnte}";
            if (d.Type == "Standard_Card")
                return $"Type='{d.Type}', Value='{d.Value}', Rank='{d.Rank}', Suit='{d.Suit}', Enchantment='{d.Enchantment}', Chip='{d.Chip}', DesireByAnte={d.DesireByAnte}";
            return $"Type='{d.Type}', Value='{d.Value}', DesireByAnte={d.DesireByAnte}";
        }
        return "OuijaConfig: " +
            $"NumNeeds={NumNeeds}, " +
            $"\tNeeds: [{string.Join(", ", Needs.Select(FormatDesire))}]" +
            $"NumWants={NumWants}, " +
            $"\tWants: [{string.Join(", ", Wants.Select(FormatDesire))}]" +
            $"MaxSearchAnte={MaxSearchAnte}, " +
            $"Deck='{Deck}', " +
            $"Stake='{Stake}', " +
            $"ScoreNaturalNegatives={ScoreNaturalNegatives}, " +
            $"ScoreDesiredNegatives={ScoreDesiredNegatives}";
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
                        return config;
                    }
                }
            }
        }
        throw new FileNotFoundException($"Could not find Ouija config: {configName}");
    }
}
