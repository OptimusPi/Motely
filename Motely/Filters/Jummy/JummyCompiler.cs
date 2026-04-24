using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Motely.Filters;

/// <summary>
/// Jummy compiles 1:1 to JAML. JUMMY ≈ Jimbo Understands My Mumbling… Yeah??
/// Supports <c>what</c>/<c>where</c> blocks and one-line <see cref="JummyMumbleLine"/> phrases
/// (e.g. Eternal Blueprint in Ante 1; Perishable Egg by Ante 4).
/// </summary>
public static class JummyCompiler
{
    private const string JummyVersionKey = "jummy";

    private static readonly Regex s_integers = new(@"\d+", RegexOptions.CultureInvariant);

    /// <summary>
    /// Compiles Jummy text to JAML (YAML) suitable for <see cref="JamlConfigLoader.TryLoad"/>.
    /// </summary>
    public static bool TryCompile(string jummy, [NotNullWhen(true)] out string? jaml, out string? error)
    {
        jaml = null;
        error = null;

        if (string.IsNullOrWhiteSpace(jummy))
        {
            error = "Jummy content is required.";
            return false;
        }

        try
        {
            var stream = new YamlStream();
            using (var reader = new StringReader(jummy))
                stream.Load(reader);

            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                error = "Jummy document must be a YAML mapping at the root.";
                return false;
            }

            var outRoot = CloneMappingTransform(root);
            var outStream = new YamlStream(new YamlDocument(outRoot));
            using var writer = new StringWriter();
            outStream.Save(writer, assignAnchors: false);
            jaml = writer.ToString();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static YamlMappingNode CloneMappingTransform(YamlMappingNode root)
    {
        var result = new YamlMappingNode();

        foreach (var child in root.Children)
        {
            if (child.Key is not YamlScalarNode keyNode || string.IsNullOrEmpty(keyNode.Value))
                continue;

            var key = keyNode.Value;
            if (string.Equals(key, JummyVersionKey, StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsClauseListKey(key) && child.Value is YamlSequenceNode seq)
            {
                result.Add(CloneScalarKey(keyNode), TransformClauseSequence(seq));
                continue;
            }

            result.Add(CloneYamlSubtree(child.Key), CloneYamlSubtree(child.Value));
        }

        return result;
    }

    private static bool IsClauseListKey(string key) =>
        string.Equals(key, "must", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "should", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "mustNot", StringComparison.OrdinalIgnoreCase);

    private static YamlSequenceNode TransformClauseSequence(YamlSequenceNode seq)
    {
        var outSeq = new YamlSequenceNode();
        foreach (var item in seq.Children)
        {
            if (item is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            {
                if (JummyMumbleLine.LooksLikeMumbleLine(scalar.Value))
                {
                    if (
                        !JummyMumbleLine.TryCompileMumbleLine(
                            scalar.Value,
                            out var mumbleClause,
                            out var mumbleError
                        )
                        || mumbleClause is null
                    )
                    {
                        throw new InvalidOperationException(
                            mumbleError ?? "Invalid jummy mumble line."
                        );
                    }

                    outSeq.Add(mumbleClause);
                    continue;
                }
            }

            if (item is YamlMappingNode map && IsWhatWhereCriterion(map))
                outSeq.Add(MergeWhatWhere(map));
            else
                outSeq.Add(CloneYamlSubtree(item));
        }

        return outSeq;
    }

    private static bool IsWhatWhereCriterion(YamlMappingNode map) =>
        TryGetChild(map, "what", out _, out var whatNode)
        && whatNode is YamlMappingNode
        && TryGetChild(map, "where", out _, out var whereNode)
        && whereNode is YamlMappingNode;

    private static YamlMappingNode MergeWhatWhere(YamlMappingNode criterion)
    {
        TryGetChild(criterion, "what", out _, out var whatNode);
        TryGetChild(criterion, "where", out _, out var whereNode);

        var what = (YamlMappingNode)whatNode!;
        var where = (YamlMappingNode)whereNode!;

        var merged = new YamlMappingNode();
        foreach (var kvp in what.Children)
            merged.Add(CloneYamlSubtree(kvp.Key), CloneYamlSubtree(kvp.Value));

        ApplyWhere(merged, (YamlMappingNode)where);

        foreach (var kvp in criterion.Children)
        {
            if (kvp.Key is not YamlScalarNode kn)
                continue;
            if (string.Equals(kn.Value, "what", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kn.Value, "where", StringComparison.OrdinalIgnoreCase))
                continue;

            merged.Add(CloneYamlSubtree(kvp.Key), CloneYamlSubtree(kvp.Value));
        }

        return merged;
    }

    private static void ApplyWhere(YamlMappingNode merged, YamlMappingNode where)
    {
        foreach (var kvp in where.Children)
        {
            if (kvp.Key is not YamlScalarNode kn || string.IsNullOrEmpty(kn.Value))
                continue;

            var k = kn.Value.Trim();
            switch (k.ToLowerInvariant())
            {
                case "ante":
                case "antes":
                    merged.Children[new YamlScalarNode("antes")] = BuildIntSequence(ParseAnteValues(kvp.Value));
                    break;
                case "booster packs":
                case "boosterpacks":
                case "packs":
                case "booster pack":
                    merged.Children[new YamlScalarNode("boosterPacks")] = BuildIntSequence(
                        ParseOrdinalOrIntValues(kvp.Value, maxBoosterOrShop: 5)
                    );
                    break;
                case "shop":
                case "shop items":
                case "shopitems":
                case "shop slots":
                    merged.Children[new YamlScalarNode("shopItems")] = BuildIntSequence(
                        ParseOrdinalOrIntValues(kvp.Value, maxBoosterOrShop: 4)
                    );
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown 'where' key '{k}'. Supported: ante, booster packs, shop (items)."
                    );
            }
        }
    }

    private static YamlSequenceNode BuildIntSequence(int[] values)
    {
        var seq = new YamlSequenceNode();
        foreach (var v in values)
            seq.Add(new YamlScalarNode(v.ToString(CultureInfo.InvariantCulture)));
        return seq;
    }

    private static int[] ParseAnteValues(YamlNode node) =>
        node switch
        {
            YamlScalarNode s => ParseAnteString(s.Value ?? ""),
            YamlSequenceNode seq => seq.Children.OfType<YamlScalarNode>().Select(ParseInt).ToArray(),
            _ => throw new InvalidOperationException("ante/antes must be a scalar or integer sequence."),
        };

    private static int[] ParseAnteString(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return [];

        var ints = new List<int>();
        foreach (Match m in s_integers.Matches(text))
        {
            if (int.TryParse(m.Value, CultureInfo.InvariantCulture, out var n))
                ints.Add(n);
        }

        if (ints.Count == 0)
            throw new InvalidOperationException($"Could not parse any ante numbers from '{text}'.");
        return ints.Distinct().Order().ToArray();
    }

    private static int[] ParseOrdinalOrIntValues(YamlNode node, int maxBoosterOrShop) =>
        node switch
        {
            YamlScalarNode s => ParseOrdinalOrIntString(s.Value ?? "", maxBoosterOrShop),
            YamlSequenceNode seq =>
                seq.Children.SelectMany(n => ParseOneSlot(n, maxBoosterOrShop)).Distinct().Order().ToArray(),
            _ => throw new InvalidOperationException("Expected a scalar or sequence for slot list."),
        };

    private static IEnumerable<int> ParseOneSlot(YamlNode n, int maxBoosterOrShop) =>
        n switch
        {
            YamlScalarNode s => ParseOrdinalOrIntString(s.Value ?? "", maxBoosterOrShop),
            _ => throw new InvalidOperationException("Slot list entries must be scalars."),
        };

    private static int[] ParseOrdinalOrIntString(string text, int max)
    {
        text = text.Trim();
        if (text.Length == 0)
            return [];

        if (text.Contains(',') || text.Contains(" and ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = Regex.Split(text, @"\s*,\s*|\s+and\s+", RegexOptions.IgnoreCase);
            var list = new List<int>();
            foreach (var p in parts)
            {
                foreach (var n in ParseSingleSlotToken(p.Trim(), max))
                    list.Add(n);
            }

            return list.Distinct().Order().ToArray();
        }

        return ParseSingleSlotToken(text, max).Distinct().Order().ToArray();
    }

    private static IEnumerable<int> ParseSingleSlotToken(string token, int max)
    {
        token = token.Trim();
        if (token.Length == 0)
            yield break;

        if (int.TryParse(token, CultureInfo.InvariantCulture, out var direct))
        {
            if (direct < 0)
                throw new InvalidOperationException($"Slot index {direct} is negative; must be >= 0.");
            if (direct > max)
            {
                // WARN rather than throw — the user might intentionally probe beyond the
                // recommended range (e.g. to check what a hypothetical extra pack slot rolls).
                Console.Error.WriteLine(
                    $"[jummy warn] Slot index {direct} exceeds recommended max {max}; continuing anyway."
                );
            }
            yield return direct;
            yield break;
        }

        var lower = token.ToLowerInvariant();
        var ord = lower switch
        {
            "first" => 0,
            "second" => 1,
            "third" => 2,
            "fourth" => 3,
            "fifth" => 4,
            "sixth" => 5,
            _ => -1,
        };

        if (ord >= 0)
        {
            if (ord > max)
            {
                Console.Error.WriteLine(
                    $"[jummy warn] Ordinal '{token}' maps to index {ord}; max recommended is {max}. Continuing anyway."
                );
            }
            yield return ord;
            yield break;
        }

        throw new InvalidOperationException(
            $"Unrecognized slot '{token}'. Use 0–{max} or first/sixth ordinals."
        );
    }

    private static int ParseInt(YamlScalarNode s)
    {
        if (!int.TryParse(s.Value, CultureInfo.InvariantCulture, out var n))
            throw new InvalidOperationException($"Not an integer: '{s.Value}'.");
        return n;
    }

    private static bool TryGetChild(
        YamlMappingNode map,
        string key,
        [NotNullWhen(true)] out YamlScalarNode? keyNode,
        [NotNullWhen(true)] out YamlNode? valueNode
    )
    {
        foreach (var child in map.Children)
        {
            if (child.Key is YamlScalarNode sn
                && string.Equals(sn.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                keyNode = sn;
                valueNode = child.Value;
                return true;
            }
        }

        keyNode = null;
        valueNode = null;
        return false;
    }

    private static YamlNode CloneScalarKey(YamlScalarNode key) => new YamlScalarNode(key.Value ?? "") { Style = key.Style };

    private static YamlNode CloneYamlSubtree(YamlNode node) =>
        node switch
        {
            YamlScalarNode s => new YamlScalarNode(s.Value ?? "") { Style = s.Style },
            YamlSequenceNode seq => new YamlSequenceNode(seq.Select(CloneYamlSubtree)),
            YamlMappingNode map => new YamlMappingNode(
                map.Children.Select(kvp =>
                    new KeyValuePair<YamlNode, YamlNode>(
                        CloneYamlSubtree(kvp.Key),
                        CloneYamlSubtree(kvp.Value)
                    )
                )
            ),
            _ => throw new InvalidOperationException($"Unsupported YAML node: {node.GetType().Name}"),
        };
}
