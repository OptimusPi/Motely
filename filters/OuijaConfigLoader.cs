using System.Text.Json;
using System.Text.Json.Serialization;
using Motely;

namespace Motely.Filters;

/// <summary>
/// Simple config loader that maintains backward compatibility while providing a clean API
/// </summary>
public static class OuijaConfigLoader
{
    /// <summary>
    /// Parse all string values to enums during config load
    /// </summary>
    private static void ParseAllEnums(OuijaConfig config)
    {
        // Parse deck and stake
        if (!string.IsNullOrEmpty(config.Deck))
        {
            if (MotelyEnumUtil.TryParseEnum<MotelyDeck>(config.Deck, out var deck))
                config.ParsedDeck = deck;
            else
                Console.WriteLine($"[WARNING] Unknown deck: {0}, using RedDeck", config.Deck);
        }
        
        if (!string.IsNullOrEmpty(config.Stake))
        {
            if (MotelyEnumUtil.TryParseEnum<MotelyStake>(config.Stake, out var stake))
                config.ParsedStake = stake;
            else
                Console.WriteLine($"[WARNING] Unknown stake: {0}, using WhiteStake", config.Stake);
        }

        // Parse all desires (needs and wants)
        if (config.Needs != null)
        {
            foreach (var need in config.Needs)
            {
                ParseDesireEnums(need);
            }
        }

        if (config.Wants != null)
        {
            foreach (var want in config.Wants)
            {
                ParseDesireEnums(want);
            }
        }
    }

    private static void ParseDesireEnums(OuijaConfig.Desire desire)
    {
        if (desire == null) return;

        // Support 'Stickers' as alias for 'JokerStickers' if present
        if (desire.JokerStickers.Count == 0 && desire.GetType().GetProperty("Stickers") != null)
        {
            var stickersProp = desire.GetType().GetProperty("Stickers");
            var stickersValue = stickersProp?.GetValue(desire) as IEnumerable<string>;
            if (stickersValue != null)
            {
                desire.JokerStickers.AddRange(stickersValue);
            }
        }

        // Parse type to TypeCategory
        if (!string.IsNullOrEmpty(desire.Type) && !desire.TypeCategory.HasValue)
        {
            if (TryParseTypeCategory(desire.Type, out var typeCat))
            {
                desire.TypeCategory = typeCat;
            }
        }

        // Parse specific enums based on type
        if (!string.IsNullOrEmpty(desire.Value))
        {
            // Parse Joker (including SoulJoker)
            if (desire.Type?.Contains("Joker") == true || desire.TypeCategory == MotelyItemTypeCategory.Joker ||
                string.Equals(desire.Type, "SoulJoker", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<MotelyJoker>(desire.Value, true, out var joker))
                {
                    desire.JokerEnum = joker;
                }
            }
            // Parse Tarot
            else if (desire.Type?.Contains("Tarot") == true || desire.TypeCategory == MotelyItemTypeCategory.TarotCard)
            {
                if (Enum.TryParse<MotelyTarotCard>(desire.Value, true, out var tarot))
                    desire.TarotEnum = tarot;
            }
            // Parse Planet
            else if (desire.Type?.Contains("Planet") == true || desire.TypeCategory == MotelyItemTypeCategory.PlanetCard)
            {
                if (Enum.TryParse<MotelyPlanetCard>(desire.Value, true, out var planet))
                    desire.PlanetEnum = planet;
            }
            // Parse Spectral
            else if (desire.Type?.Contains("Spectral") == true || desire.TypeCategory == MotelyItemTypeCategory.SpectralCard)
            {
                if (Enum.TryParse<MotelySpectralCard>(desire.Value, true, out var spectral))
                    desire.SpectralEnum = spectral;
            }
            // Parse Tag
            else if (desire.Type?.Equals("Tag", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (Enum.TryParse<MotelyTag>(desire.Value, true, out var tag))
                    desire.TagEnum = tag;
            }
            // Parse Voucher
            else if (desire.Type?.Equals("Voucher", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (MotelyEnumUtil.TryParseEnum<MotelyVoucher>(desire.Value, out var voucher))
                {
                    desire.VoucherEnum = voucher;
                }
                else
                {
                    var validNames = string.Join(", ", MotelyEnumUtil.GetAllCanonicalNames<MotelyVoucher>());
                    Console.WriteLine($"[WARNING] Could not map voucher '{desire.Value}' to MotelyVoucher enum. Valid values: {validNames}");
                }
            }
        }

        // Parse Edition if present - SINGLE LOCATION FOR EDITION PARSING
        if (!string.IsNullOrEmpty(desire.Edition) && !desire.Edition.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            desire.ParsedEdition = desire.Edition.ToLowerInvariant() switch
            {
                "foil" => MotelyItemEdition.Foil,
                "holographic" => MotelyItemEdition.Holographic,
                "polychrome" => MotelyItemEdition.Polychrome,
                "negative" => MotelyItemEdition.Negative,
                _ => null
            };
            
            DebugLogger.LogFormat("[ParseDesireEnums] Parsed edition '{0}' to {1}", 
                desire.Edition, desire.ParsedEdition?.ToString() ?? "null");
        }
    }

    public static bool TryParseTypeCategory(string value, out MotelyItemTypeCategory type)
    {
        type = default;
        if (string.IsNullOrEmpty(value))
            return false;
            
        if (MotelyEnumUtil.TryParseEnum<MotelyItemTypeCategory>(value, out type))
            return true;
        
        // Handle normalized type names
        var normalized = value.Replace("Card", "");
        if (MotelyEnumUtil.TryParseEnum<MotelyItemTypeCategory>(normalized + "Card", out type))
            return true;
            
        return false;
    }
    /// <summary>
    /// Load config from file with multiple path attempts
    /// </summary>
    public static OuijaConfig Load(string configName)
    {
        string[] searchPaths =
        {
            Path.Combine(AppContext.BaseDirectory, "JsonItemFilters", configName),
            Path.Combine(AppContext.BaseDirectory, "JsonItemFilters", configName + ".json"),
            Path.Combine(AppContext.BaseDirectory, "JsonItemFilters", configName + ".ouija.json"),
            Path.Combine(".", "JsonItemFilters", configName),
            Path.Combine(".", "JsonItemFilters", configName + ".json"),
            Path.Combine(".", "JsonItemFilters", configName + ".ouija.json"),
            configName,
            configName + ".json",
            configName + ".ouija.json"
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"Loading config from: {path}");
                var config = OuijaConfig.LoadFromJson(path);
                
                // Parse all string values to enums at load time
                ParseAllEnums(config);
                
                return config;
            }
        }

        throw new FileNotFoundException($"Could not find Ouija config: {configName}");
    }

    /// <summary>
    /// Save config to JSON file
    /// </summary>
    public static void Save(OuijaConfig config, string path)
    {
        var json = config.ToJson();
        File.WriteAllText(path, json);
        Console.WriteLine($"Saved config to: {path}");
    }

    /// <summary>
    /// Create and save an example config
    /// </summary>
    public static void CreateExampleConfig(string path = "example.ouija.json")
    {
        var example = OuijaConfig.CreateExample();
        Save(example, path);
    }
}
