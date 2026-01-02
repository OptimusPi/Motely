using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Motely.Filters;

/// <summary>
/// SINGLE SOURCE OF TRUTH for JAML formatting.
/// Used by Desktop app, API, and anywhere else that needs to format JAML.
/// 
/// Produces clean, idiomatic JAML output:
/// - Uses type-as-key format: "joker: Blueprint" instead of "type: Joker, value: Blueprint"
/// - Collapses numeric arrays to inline: "antes: [1,2,3]" instead of multi-line
/// - Preserves object arrays (like criteria/clauses) as multi-line
/// - Omits null/empty/default properties
/// </summary>
public static class JamlFormatter
{
    // Properties that should have inline numeric arrays
    private static readonly HashSet<string> InlineArrayProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "antes",
        "shopSlots",
        "packSlots",
        "shopslots",
        "packslots",
        "shop_slots",
        "pack_slots",
        "stickers"
    };

    // Valid type names that can be used as keys (type-as-key format)
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "joker", "souljoker", "tarot", "planet", "spectral", "voucher",
        "tag", "blind", "boss", "card", "playingcard", "standardcard", "event",
        "tarotcard", "planetcard", "spectralcard"
    };

    /// <summary>
    /// Format a MotelyJsonConfig to clean JAML string
    /// </summary>
    public static string Format(MotelyJsonConfig config)
    {
        if (config == null)
            return string.Empty;

        // First serialize with YamlDotNet
        // Use NullNamingConvention to match JamlConfigLoader's deserialization setup
        // This ensures round-trip compatibility - properties are matched by YamlMember aliases
        var serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull | 
                DefaultValuesHandling.OmitEmptyCollections | 
                DefaultValuesHandling.OmitDefaults)
            .Build();

        var yaml = serializer.Serialize(config);
        
        // Post-process for JAML idioms
        return PostProcess(yaml);
    }

    /// <summary>
    /// Format raw JAML string (parse then re-serialize with formatting)
    /// </summary>
    public static string Format(string jamlContent)
    {
        if (string.IsNullOrWhiteSpace(jamlContent))
            return jamlContent ?? string.Empty;

        var config = Parse(jamlContent);
        return Format(config);
    }

    /// <summary>
    /// Parse JAML string to MotelyJsonConfig
    /// </summary>
    public static MotelyJsonConfig Parse(string jamlContent)
    {
        if (string.IsNullOrWhiteSpace(jamlContent))
            return new MotelyJsonConfig();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<MotelyJsonConfig>(jamlContent) ?? new MotelyJsonConfig();
    }

    /// <summary>
    /// Extension method: config.ToJaml()
    /// </summary>
    public static string ToJaml(this MotelyJsonConfig config) => Format(config);

    private static string PostProcess(string yaml)
    {
        var lines = yaml.Split('\n');
        var result = new StringBuilder();
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            var indent = line.Length - trimmed.Length;
            var indentStr = new string(' ', indent);

            // Check for type-as-key conversion pattern
            // Look for "- type: X" followed by "  value: Y"
            if (trimmed.StartsWith("- type:", StringComparison.OrdinalIgnoreCase))
            {
                var typeMatch = Regex.Match(trimmed, @"^-\s*type:\s*['""]?(\w+)['""]?\s*$", RegexOptions.IgnoreCase);
                if (typeMatch.Success && i + 1 < lines.Length)
                {
                    var typeName = typeMatch.Groups[1].Value.ToLowerInvariant();
                    var nextLine = lines[i + 1];
                    var nextTrimmed = nextLine.TrimStart();
                    
                    // Check if next line is "value: X"
                    var valueMatch = Regex.Match(nextTrimmed, @"^value:\s*(.+)$", RegexOptions.IgnoreCase);
                    if (valueMatch.Success && ValidTypes.Contains(typeName))
                    {
                        var value = valueMatch.Groups[1].Value.Trim();
                        // Remove quotes if present
                        if ((value.StartsWith("'") && value.EndsWith("'")) ||
                            (value.StartsWith("\"") && value.EndsWith("\"")))
                        {
                            value = value[1..^1];
                        }
                        
                        // Write type-as-key format
                        result.AppendLine($"{indentStr}- {typeName}: {value}");
                        i += 2; // Skip both lines
                        continue;
                    }
                }
            }

            // Check for numeric array properties that should be inlined
            var arrayPropMatch = Regex.Match(trimmed, @"^(\w+):\s*$");
            if (arrayPropMatch.Success && InlineArrayProperties.Contains(arrayPropMatch.Groups[1].Value))
            {
                var propName = arrayPropMatch.Groups[1].Value;
                var values = new List<string>();
                var j = i + 1;
                
                // Collect array items
                while (j < lines.Length)
                {
                    var itemLine = lines[j];
                    var itemTrimmed = itemLine.TrimStart();
                    var itemIndent = itemLine.Length - itemTrimmed.Length;
                    
                    // Check if this is an array item at the correct indent level
                    if (itemIndent > indent && itemTrimmed.StartsWith("- "))
                    {
                        var itemValue = itemTrimmed[2..].Trim();
                        // Only inline if it's a simple value (number or short string)
                        if (int.TryParse(itemValue, out _) || 
                            (itemValue.StartsWith("'") && itemValue.EndsWith("'")) ||
                            (itemValue.StartsWith("\"") && itemValue.EndsWith("\"")) ||
                            (!itemValue.Contains(':') && itemValue.Length < 30))
                        {
                            // Remove quotes for numbers
                            if (int.TryParse(itemValue.Trim('\'', '"'), out var num))
                            {
                                values.Add(num.ToString());
                            }
                            else
                            {
                                values.Add(itemValue.Trim('\'', '"'));
                            }
                            j++;
                            continue;
                        }
                    }
                    break;
                }
                
                if (values.Count > 0)
                {
                    // Write inline array (compact, no spaces after commas)
                    result.AppendLine($"{indentStr}{propName}: [{string.Join(",", values)}]");
                    i = j;
                    continue;
                }
            }

            // Check for already-inline arrays that need reformatting (remove spaces)
            var inlineArrayMatch = Regex.Match(trimmed, @"^(\w+):\s*\[\s*(.+?)\s*\]\s*$");
            if (inlineArrayMatch.Success && InlineArrayProperties.Contains(inlineArrayMatch.Groups[1].Value))
            {
                var propName = inlineArrayMatch.Groups[1].Value;
                var arrayContent = inlineArrayMatch.Groups[2].Value;
                // Remove spaces after commas for compact format
                var compactContent = Regex.Replace(arrayContent, @",\s+", ",");
                result.AppendLine($"{indentStr}{propName}: [{compactContent}]");
                i++;
                continue;
            }

            // Default: keep line as-is
            result.AppendLine(line);
            i++;
        }

        return result.ToString().TrimEnd() + "\n";
    }
}

