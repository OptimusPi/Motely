using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MotelyJaml;
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
    // Constants for array inlining validation
    private const int MaxInlineStringLength = 50;
    private const int MaxSimpleValueLength = 30;

    private static string QuoteYamlString(string value)
    {
        if (value == null)
            return "\"\"";

        // Use double-quoted YAML scalars and escape the minimal set we might encounter.
        // This keeps output round-trip safe when we inline arrays like stickers.
        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");

        return $"\"{escaped}\"";
    }

    private static readonly HashSet<string> InlineArrayProperties = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "antes",
        "values",
        "wantedantes",
        "wantedshopslots",
        "wantedpackslots",
        "shopslots",
        "packslots",
        "shop_slots",
        "pack_slots",
        "stickers",
    };

    // Valid type names that can be used as keys (type-as-key format)
    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "joker",
        "souljoker",
        "tarot",
        "planet",
        "spectral",
        "voucher",
        "tag",
        "blind",
        "boss",
        "card",
        "playingcard",
        "standardcard",
        "event",
        "tarotcard",
        "planetcard",
        "spectralcard",
    };

    // Shared regex patterns for JAML processing
    private static class Patterns
    {
        public static readonly string TypeAsKeyMatch = @"^-\s*type:\s*['""]?(\w+)['""]?\s*$";
        public static readonly string ValueMatch = @"^value:\s*(.+)$";
        public static readonly string ArrayPropertyMatch = @"^(\w+):\s*$";
        public static readonly string InlineArrayMatch = @"^(\w+):\s*\[\s*(.+?)\s*\]\s*$";
    }

    /// <summary>
    /// Format a MotelyJsonConfig to clean JAML string
    /// </summary>
    public static string Format(MotelyJsonConfig config)
    {
        if (config == null)
            return string.Empty;

        // AOT-compatible: Use StaticSerializerBuilder with pre-generated context
        var serializer = new StaticSerializerBuilder(new MotelyJamlStaticContext())
            .WithNamingConvention(NullNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull
                    | DefaultValuesHandling.OmitEmptyCollections
                    | DefaultValuesHandling.OmitDefaults
            )
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

        // AOT-compatible: Use StaticDeserializerBuilder with pre-generated context
        var deserializer = new StaticDeserializerBuilder(new MotelyJamlStaticContext())
            .WithNamingConvention(NullNamingConvention.Instance)
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
                var typeMatch = Regex.Match(
                    trimmed,
                    Patterns.TypeAsKeyMatch,
                    RegexOptions.IgnoreCase
                );
                if (typeMatch.Success && i + 1 < lines.Length)
                {
                    var typeName = typeMatch.Groups[1].Value.ToLowerInvariant();
                    var nextLine = lines[i + 1];
                    var nextTrimmed = nextLine.TrimStart();

                    // Check if next line is "value: X"
                    var valueMatch = Regex.Match(
                        nextTrimmed,
                        Patterns.ValueMatch,
                        RegexOptions.IgnoreCase
                    );
                    if (valueMatch.Success && ValidTypes.Contains(typeName))
                    {
                        var value = valueMatch.Groups[1].Value.Trim();
                        // Remove quotes if present
                        if (
                            (value.StartsWith("'") && value.EndsWith("'"))
                            || (value.StartsWith("\"") && value.EndsWith("\""))
                        )
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

            // Check for array properties that should be inlined (numeric arrays, string arrays like stickers)
            // Match: "propName:" or "  propName:" (with optional trailing whitespace)
            var arrayPropMatch = Regex.Match(trimmed, Patterns.ArrayPropertyMatch);
            if (
                arrayPropMatch.Success
                && InlineArrayProperties.Contains(arrayPropMatch.Groups[1].Value)
            )
            {
                var propName = arrayPropMatch.Groups[1].Value;
                var values = new List<string>();
                var j = i + 1;

                // Collect array items that follow this property
                while (j < lines.Length)
                {
                    var itemLine = lines[j];
                    if (string.IsNullOrWhiteSpace(itemLine))
                    {
                        j++;
                        continue;
                    }

                    var itemTrimmed = itemLine.TrimStart();
                    var itemIndent = itemLine.Length - itemTrimmed.Length;

                    // Stop if we hit a line at same or less indent (end of array, next property)
                    if (itemIndent <= indent)
                    {
                        break;
                    }

                    // Check if this is an array item (starts with "- " at deeper indent)
                    if (itemTrimmed.StartsWith("- "))
                    {
                        var itemValue = itemTrimmed[2..].Trim();

                        // Accept simple values: numbers, quoted strings, or short unquoted strings
                        // For stickers and other string arrays, accept unquoted strings
                        bool isValidValue =
                            int.TryParse(itemValue, out _)
                            || (itemValue.StartsWith("'") && itemValue.EndsWith("'"))
                            || (itemValue.StartsWith("\"") && itemValue.EndsWith("\""))
                            || (
                                !itemValue.Contains(':')
                                && !itemValue.Contains('[')
                                && !itemValue.Contains('{')
                                && !itemValue.Contains('}')
                                && !itemValue.Contains(']')
                                && itemValue.Length < MaxInlineStringLength
                            );

                        if (isValidValue)
                        {
                            // Remove quotes if present
                            var cleanValue = itemValue.Trim('\'', '"');
                            values.Add(cleanValue);
                            j++;
                            continue;
                        }
                    }

                    // If we get here, it's not an array item, stop collecting
                    break;
                }

                if (values.Count > 0)
                {
                    // Only inline numeric arrays - string arrays must stay multi-line for YamlDotNet compatibility
                    // Check if all values are numeric
                    bool allNumeric = true;
                    foreach (var v in values)
                    {
                        if (!int.TryParse(v, out _))
                        {
                            allNumeric = false;
                            break;
                        }
                    }

                    if (allNumeric)
                    {
                        // Write inline numeric array (compact, no spaces after commas)
                        result.AppendLine($"{indentStr}{propName}: [{string.Join(",", values)}]");
                        i = j;
                        continue;
                    }
                    // If not all numeric, fall through to default behavior (keep as multi-line)
                }
            }

            // Check for already-inline arrays that need reformatting (remove spaces)
            // Only process numeric arrays - string arrays (like stickers) must stay multi-line
            var inlineArrayMatch = Regex.Match(trimmed, Patterns.InlineArrayMatch);
            if (
                inlineArrayMatch.Success
                && InlineArrayProperties.Contains(inlineArrayMatch.Groups[1].Value)
            )
            {
                var propName = inlineArrayMatch.Groups[1].Value;
                var arrayContent = inlineArrayMatch.Groups[2].Value;
                // Remove spaces after commas for compact format (numeric arrays only)
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
