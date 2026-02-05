using Motely.Filters;
using Motely.Filters.MotelyJson;
using MotelyJaml;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Motely;

/// <summary>
/// JAML (Joker Ante Markup Language) configuration loader.
/// JAML is a YAML-based format specifically designed for Balatro seed filter configuration.
/// </summary>
public static class JamlConfigLoader
{
    /// <summary>
    /// Try to load a MotelyJsonConfig from a JAML file.
    /// </summary>
    public static bool TryLoadFromJaml(
        string jamlPath,
        out MotelyJsonConfig? config,
        out string? error
    )
    {
        config = null;
        error = null;

        if (!File.Exists(jamlPath))
        {
            error = $"File not found: {jamlPath}";
            return false;
        }

        try
        {
            var jamlContent = File.ReadAllText(jamlPath);
            return TryLoadFromJamlString(jamlContent, out config, out error);
        }
        catch (Exception ex)
        {
            config = null;
            error = $"Failed to read JAML file: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Try to load a MotelyJsonConfig from a JAML string.
    /// </summary>
    public static bool TryLoadFromJamlString(
        string jamlContent,
        out MotelyJsonConfig? config,
        out string? error
    )
    {
        config = null;
        error = null;

        try
        {
            // Pre-process JAML ONLY for type-as-key syntax expansion (Joker: Showman -> type: Joker, value: Showman)
            // YamlDotNet handles case-insensitive matching natively via .WithCaseInsensitivePropertyMatching()
            jamlContent = PreProcessJamlForTypeAsKey(jamlContent);

            // AOT-compatible: Use StaticDeserializerBuilder with pre-generated context
            // Note: WithNodeDeserializer and WithCaseInsensitivePropertyMatching are supported by StaticDeserializerBuilder
            var deserializer = new StaticDeserializerBuilder(new MotelyJamlStaticContext())
                .WithNamingConvention(NullNamingConvention.Instance)
                .WithCaseInsensitivePropertyMatching()
                .WithNodeDeserializer(
                    new JamlTypeAsKeyNodeDeserializer(),
                    s =>
                        s.Before<YamlDotNet.Serialization.NodeDeserializers.ObjectNodeDeserializer>()
                )
                .Build();

            // Wrap with MergingParser to support YAML merge keys (<<)
            var parser = new Parser(new StringReader(jamlContent));
            var mergingParser = new MergingParser(parser);
            var deserializedConfig = deserializer.Deserialize<MotelyJsonConfig>(mergingParser);

            if (deserializedConfig == null)
            {
                error = "Failed to deserialize JAML - result was null";
                return false;
            }

            deserializedConfig.PostProcess();

            // Validate config
            MotelyJsonConfigValidator.ValidateConfig(deserializedConfig);

            config = deserializedConfig;
            return true;
        }
        catch (Exception ex)
        {
            config = null;
            var innerMsg = ex.InnerException?.Message;
            var details = innerMsg != null ? $" -> {innerMsg}" : "";

            // Extract line number from YAML parser exceptions
            var lineInfo = "";
            var lineNumber = 0;
            if (
                ex.Message.Contains("Line:")
                || ex.Message.Contains("at Line")
                || ex.Message.Contains("line")
            )
            {
                var lineMatch = System.Text.RegularExpressions.Regex.Match(
                    ex.Message,
                    @"[Ll]ine[:\s]+(\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out lineNumber))
                {
                    lineInfo = $" (Line {lineNumber})";
                }
            }

            // Build helpful error message
            var errorMsg = new System.Text.StringBuilder();
            errorMsg.AppendLine($"Failed to parse JAML{lineInfo}: {ex.Message}{details}");

            // Check for specific error patterns and provide targeted hints
            var errorLower = ex.Message.ToLowerInvariant();
            if (
                errorLower.Contains("did not find expected key")
                || errorLower.Contains("block mapping")
            )
            {
                errorMsg.AppendLine(
                    "  ❌ Missing space after colon (e.g., 'antes:[1]' should be 'antes: [1]')"
                );
                errorMsg.AppendLine("  ❌ Incorrect indentation (use 2 spaces, not tabs)");
                errorMsg.AppendLine("  ❌ Missing colon after key");

                // Try to show the problematic line if we have a line number
                if (lineNumber > 0)
                {
                    var lines = jamlContent.Split('\n');
                    if (lineNumber <= lines.Length)
                    {
                        var problemLine = lines[lineNumber - 1];
                        errorMsg.AppendLine(
                            $"  📍 Problematic line {lineNumber}: {problemLine.Trim()}"
                        );
                    }
                }
            }
            else if (errorLower.Contains("sequence") || errorLower.Contains("array"))
            {
                errorMsg.AppendLine("  ❌ Array syntax error - use brackets: [item1, item2]");
                errorMsg.AppendLine("  ❌ Missing comma between array items");
            }
            else if (errorLower.Contains("indentation") || errorLower.Contains("indent"))
            {
                errorMsg.AppendLine("  ❌ Incorrect indentation - YAML requires consistent spacing");
                errorMsg.AppendLine("  ❌ Use 2 spaces per indentation level (not tabs)");
            }
            else
            {
                errorMsg.AppendLine("  ❌ Check YAML syntax (indentation, colons, brackets)");
                errorMsg.AppendLine("  ❌ Verify property names match schema");
                errorMsg.AppendLine("  ❌ Ensure array properties use [] brackets");
            }

            errorMsg.AppendLine();
            errorMsg.AppendLine("💡 Quick fixes:");
            errorMsg.AppendLine(
                "  • Always put a space after colons: 'key: value' not 'key:value'"
            );
            errorMsg.AppendLine("  • Use consistent 2-space indentation");
            errorMsg.AppendLine("  • Arrays: 'antes: [1, 2, 3]' not 'antes: 1, 2, 3'");

            error = errorMsg.ToString();
            return false;
        }
    }

    /// <summary>
    /// Pre-process JAML ONLY for type-as-key syntax expansion (Joker: Showman -> type: Joker, value: Showman).
    /// Case-insensitive property matching is handled by YamlDotNet's .WithCaseInsensitivePropertyMatching().
    /// </summary>
    private static string PreProcessJamlForTypeAsKey(string jamlContent)
    {
        var lines = jamlContent.Split('\n');
        var result = new System.Text.StringBuilder();

        // Support clean type-as-key syntax: "joker: Blueprint" instead of "type: Joker, value: Blueprint"
        // Support plural values arrays: "jokers: [Blueprint, Brainstorm]" expands to multiple clauses
        // Singular type keys (case-insensitive matching handled via ToLowerInvariant)
        var typeKeys = new[]
        {
            "joker",
            "souljoker",
            "voucher",
            "tarot",
            "tarotcard",
            "planet",
            "planetcard",
            "spectral",
            "spectralcard",
            "standardcard",
            "boss",
            "tag",
            "smallblindtag",
            "bigblindtag",
            "erraticrank",
            "erraticsuit",
            "event",
            "and",
            "or",
        };

        // Plural type keys for array syntax (case-insensitive)
        var pluralTypeKeys = new[]
        {
            "jokers",
            "souljokers",
            "vouchers",
            "tarots",
            "tarotcards",
            "planets",
            "planetcards",
            "spectrals",
            "spectralcards",
            "standardcards",
            "bosses",
            // "tags" and "events" removed as they are too common in source properties
            "smallblindtags",
            "bigblindtags",
            "erraticranks",
            "erraticsuits",
        };

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            bool matched = false;

            // Check if line has type-as-key pattern (e.g., "  - joker: Blueprint" or "    - joker: Blueprint")
            // Match ANY case variation: "Joker:", "JOKER:", "joker:", etc.
            if (trimmed.StartsWith("- ", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the key part (everything between "- " and ":")
                var colonIndex = trimmed.IndexOf(':', 2);
                if (colonIndex > 2)
                {
                    var keyPart = trimmed.Substring(2, colonIndex - 2).Trim().ToLowerInvariant();

                    // Handle plural arrays (jokers: [Blueprint, Brainstorm])
                    foreach (var pluralKey in pluralTypeKeys)
                    {
                        if (keyPart == pluralKey.ToLowerInvariant())
                        {
                            var indent = line.Substring(0, line.IndexOf('-'));
                            var singularType = GetSingularTypeName(pluralKey);
                            var normalizedType = NormalizeTypeName(singularType);
                            var arrayContent = trimmed.Substring(colonIndex + 1).Trim();

                            // Convert jokers: [Blueprint, Brainstorm] to type: Joker + values: [Blueprint, Brainstorm]
                            result.AppendLine($"{indent}- type: {normalizedType}");
                            result.AppendLine($"{indent}  values: {arrayContent}");
                            matched = true;
                            break;
                        }
                    }

                    // Special handling for "tags" and "events" - only treat as type-as-key if value is an array
                    // This avoids conflicts with the boolean Tags property in SourcesConfig
                    if (!matched && (keyPart == "tags" || keyPart == "events"))
                    {
                        var valueContent = trimmed.Substring(colonIndex + 1).Trim();
                        // Remove inline comments before checking if it's an array
                        var commentIndex = valueContent.IndexOf('#');
                        if (commentIndex >= 0)
                            valueContent = valueContent.Substring(0, commentIndex).Trim();

                        if (valueContent.StartsWith('['))
                        {
                            var indent = line.Substring(0, line.IndexOf('-'));
                            var normalizedType = keyPart == "tags" ? "Tag" : "Event";
                            var originalValue = trimmed.Substring(colonIndex + 1).Trim();
                            result.AppendLine($"{indent}- type: {normalizedType}");
                            result.AppendLine($"{indent}  values: {originalValue}");
                            matched = true;
                        }
                    }

                    // Then check for singular type-as-key patterns
                    if (!matched)
                    {
                        foreach (var typeKey in typeKeys)
                        {
                            if (keyPart == typeKey.ToLowerInvariant())
                            {
                                var indent = line.Substring(0, line.IndexOf('-'));
                                var value = trimmed.Substring(colonIndex + 1).Trim();

                                // Convert to standard format
                                var normalizedType = NormalizeTypeName(typeKey);
                                result.AppendLine($"{indent}- type: {normalizedType}");

                                // Special handling for or/and - they use "clauses:" not "value:"
                                // This allows shorthand: "- or:" followed by nested items
                                // instead of requiring explicit "clauses:" keyword
                                if (
                                    typeKey.Equals("or", StringComparison.OrdinalIgnoreCase)
                                    || typeKey.Equals("and", StringComparison.OrdinalIgnoreCase)
                                )
                                {
                                    // "null" comes from js-yaml formatter quirk - treat as empty
                                    // User already has explicit "clauses:" on next line, don't add another
                                    if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Just emit type, user has explicit clauses: below
                                    }
                                    else if (string.IsNullOrEmpty(value))
                                    {
                                        // Normal shorthand: "- or:" with nested items (no explicit clauses:)
                                        // Add "clauses:" so nested items become the clauses array
                                        result.AppendLine($"{indent}  clauses:");
                                    }
                                    else
                                    {
                                        // User wrote "- or: something" which doesn't make sense
                                        // Just pass it through and let the deserializer error
                                        result.AppendLine($"{indent}  value: {value}");
                                    }
                                }
                                else
                                {
                                    result.AppendLine($"{indent}  value: {value}");
                                }
                                matched = true;
                                break; // Found match, stop checking other typeKeys
                            }
                        }
                    }
                }
            }

            // Also handle type-as-key in nested clauses (indented, no "- " prefix)
            // Pattern: "    smallblindtag: NegativeTag" (inside clauses array)
            if (!matched && trimmed.Length > 0 && !trimmed.StartsWith("- "))
            {
                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex > 0)
                {
                    var keyPart = trimmed.Substring(0, colonIndex).Trim();

                    // Check plural keys
                    foreach (var pluralKey in pluralTypeKeys)
                    {
                        if (string.Equals(keyPart, pluralKey, StringComparison.OrdinalIgnoreCase))
                        {
                            var indent = line.Substring(0, line.Length - trimmed.Length);
                            var singularType = GetSingularTypeName(pluralKey);
                            var normalizedType = NormalizeTypeName(singularType);
                            var arrayContent = trimmed.Substring(colonIndex + 1).Trim();

                            result.AppendLine($"{indent}type: {normalizedType}");
                            result.AppendLine($"{indent}values: {arrayContent}");
                            matched = true;
                            break;
                        }
                    }

                    // Special handling for "tags" and "events" - only treat as type-as-key if value is an array
                    // This avoids conflicts with the boolean Tags property in SourcesConfig
                    if (
                        !matched
                        && (
                            string.Equals(keyPart, "tags", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(keyPart, "events", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        var valueContent = trimmed.Substring(colonIndex + 1).Trim();
                        // Remove inline comments before checking if it's an array
                        var commentIndex = valueContent.IndexOf('#');
                        if (commentIndex >= 0)
                            valueContent = valueContent.Substring(0, commentIndex).Trim();

                        if (valueContent.StartsWith('['))
                        {
                            var indent = line.Substring(0, line.Length - trimmed.Length);
                            var normalizedType = string.Equals(
                                keyPart,
                                "tags",
                                StringComparison.OrdinalIgnoreCase
                            )
                                ? "Tag"
                                : "Event";
                            var originalValue = trimmed.Substring(colonIndex + 1).Trim();
                            result.AppendLine($"{indent}type: {normalizedType}");
                            result.AppendLine($"{indent}values: {originalValue}");
                            matched = true;
                        }
                    }

                    // Check singular keys
                    if (!matched)
                    {
                        foreach (var typeKey in typeKeys)
                        {
                            if (keyPart == typeKey.ToLowerInvariant())
                            {
                                var indent = line.Substring(0, line.Length - trimmed.Length);
                                var value = trimmed.Substring(colonIndex + 1).Trim();
                                var normalizedType = NormalizeTypeName(typeKey);

                                result.AppendLine($"{indent}type: {normalizedType}");
                                result.AppendLine($"{indent}value: {value}");
                                matched = true;
                                break;
                            }
                        }
                    }
                }
            }

            // Only append original line if no type-as-key pattern was found
            if (!matched)
            {
                result.AppendLine(line);
            }
            else
            {
                // Skip the next line if it's just the value continuation (already handled)
                // This handles multi-line values that might follow
            }
        }

        var processed = result.ToString();

        // DEBUG: Log preprocessed output if it changed
#if DEBUG
        if (processed != jamlContent)
        {
            System.Diagnostics.Debug.WriteLine("=== PREPROCESSOR OUTPUT ===");
            System.Diagnostics.Debug.WriteLine(processed);
            System.Diagnostics.Debug.WriteLine("=== END PREPROCESSOR ===");
        }
#endif
        return processed;
    }

    private static string GetSingularTypeName(string pluralKey)
    {
        // Normalize to lowercase for switch expression
        return pluralKey.ToLowerInvariant() switch
        {
            "jokers" => "joker",
            "souljokers" => "soulJoker",
            "vouchers" => "voucher",
            "tarots" or "tarotcards" => "tarot",
            "planets" or "planetcards" => "planet",
            "spectrals" or "spectralcards" => "spectral",
            "standardcards" => "standardCard",
            "bosses" => "boss",
            "tags" => "tag",
            "smallblindtags" => "smallBlindTag",
            "bigblindtags" => "bigBlindTag",
            "events" => "event",
            "erraticranks" => "erraticRank",
            "erraticsuits" => "erraticSuit",
            _ => pluralKey.TrimEnd('s'), // fallback: remove 's'
        };
    }

    private static string NormalizeTypeName(string typeKey)
    {
        return typeKey.ToLowerInvariant() switch
        {
            "joker" => "Joker",
            "souljoker" => "SoulJoker",
            "voucher" => "Voucher",
            "tarot" or "tarotcard" => "TarotCard",
            "planet" or "planetcard" => "PlanetCard",
            "spectral" or "spectralcard" => "SpectralCard",
            "standardcard" => "PlayingCard", // StandardCard maps to PlayingCard enum
            "boss" => "Boss",
            "tag" => "Tag", // Generic tag (matches both SmallBlindTag and BigBlindTag)
            "smallblindtag" => "SmallBlindTag",
            "bigblindtag" => "BigBlindTag",
            "event" => "Event",
            "erraticrank" => "ErraticRank",
            "erraticsuit" => "ErraticSuit",
            "and" => "And",
            "or" => "Or",
            _ => typeKey,
        };
    }
}
