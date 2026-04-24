using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Motely.Filters;

/// <summary>
/// JUMMY ≈ Jimbo Understands My Mumbling… Yeah??
/// One-line phrases: item text (see <see cref="FormatUtils.TryParseMotelyItem"/>)
/// plus <c> in Ante N</c> (only that ante; ante 1 uses four booster slots, later antes use six) or
/// <c> by Ante N</c> (check antes 1…N cumulatively).
/// </summary>
internal static class JummyMumbleLine
{
    private static readonly Regex s_inAnte = new(
        @"^(?<item>.+?)\s+in\s+Ante\s+(?<n>\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private static readonly Regex s_byAnte = new(
        @"^(?<item>.+?)\s+by\s+Ante\s+(?<n>\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    /// <summary>Returns true if the line looks like a mumble phrase (so we can fail with a clear error).</summary>
    internal static bool LooksLikeMumbleLine(string line)
    {
        var t = StripLineComment(line).Trim();
        return s_inAnte.IsMatch(t) || s_byAnte.IsMatch(t);
    }

    internal static bool TryCompileMumbleLine(
        string line,
        [NotNullWhen(true)] out YamlMappingNode? clause,
        out string? error
    )
    {
        clause = null;
        error = null;
        var t = StripLineComment(line).Trim();
        if (t.Length == 0)
        {
            error = "Empty jummy line.";
            return false;
        }

        Match? m = s_inAnte.Match(t);
        bool byRun = false;
        if (!m.Success)
        {
            m = s_byAnte.Match(t);
            byRun = m.Success;
        }

        if (!m.Success)
        {
            error = "Expected a jummy line like 'Eternal Blueprint in Ante 1' or 'Perishable Egg by Ante 4'.";
            return false;
        }

        var itemText = m.Groups["item"].Value.Trim();
        if (!int.TryParse(m.Groups["n"].Value, out var anteN) || anteN < 1)
        {
            error = "Ante must be a positive integer.";
            return false;
        }

        if (!FormatUtils.TryParseMotelyItem(itemText, out var item))
        {
            error = $"Could not parse item text '{itemText}' (expected a Motely item string, e.g. 'Eternal Blueprint').";
            return false;
        }

        if (item.TypeCategory != MotelyItemTypeCategory.Joker)
        {
            error = "Jummy mumble lines (v1) only support jokers.";
            return false;
        }

        // MotelyItemType is (Joker category) | MotelyJoker — must strip category before using MotelyJoker.
        var joker = (MotelyJoker)((uint)item.Type & ~(uint)MotelyItemTypeCategory.Joker);
        clause = BuildJokerClauseMapping(item, joker);

        if (byRun)
        {
            AddAnteRange(clause, 1, anteN);
            AddBoosterPacksUnion(clause);
        }
        else
        {
            clause.Children[new YamlScalarNode("antes")] = IntSequence(anteN);
            clause.Children[new YamlScalarNode("boosterPacks")] = IntSequence(
                anteN == 1 ? [0, 1, 2, 3] : [0, 1, 2, 3, 4, 5]
            );
        }

        return true;
    }

    private static string StripLineComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx < 0 ? line : line[..idx];
    }

    private static YamlMappingNode BuildJokerClauseMapping(MotelyItem item, MotelyJoker joker)
    {
        var map = new YamlMappingNode();
        var name = joker.ToString();
        var rarityBits = (uint)joker & MotelyGlobals.JokerRarityMask;

        void addRareKey(string key)
        {
            // Double-quoted so the fragment deserializer never treats the joker name as a non-string (e.g. implicit int).
            var valueNode = new YamlScalarNode(name) { Style = ScalarStyle.DoubleQuoted };
            map.Children[new YamlScalarNode(key)] = valueNode;
        }

        if (rarityBits == (uint)MotelyJokerRarity.Common)
            addRareKey("commonJoker");
        else if (rarityBits == (uint)MotelyJokerRarity.Uncommon)
            addRareKey("uncommonJoker");
        else if (rarityBits == (uint)MotelyJokerRarity.Rare)
            addRareKey("rareJoker");
        else if (rarityBits == (uint)MotelyJokerRarity.Legendary)
            addRareKey("legendaryJoker");
        else
            throw new InvalidOperationException($"Unsupported joker rarity bits: 0x{rarityBits:X}.");

        if (item.Edition != MotelyItemEdition.None)
        {
            map.Children[new YamlScalarNode("edition")] = new YamlScalarNode(item.Edition.ToString())
            {
                Style = ScalarStyle.DoubleQuoted,
            };
        }

        var stickers = new YamlSequenceNode();
        if (item.IsEternal)
            stickers.Add(
                new YamlScalarNode(MotelyJokerSticker.Eternal.ToString()) { Style = ScalarStyle.DoubleQuoted }
            );
        if (item.IsPerishable)
            stickers.Add(
                new YamlScalarNode(MotelyJokerSticker.Perishable.ToString()) { Style = ScalarStyle.DoubleQuoted }
            );
        if (item.IsRental)
            stickers.Add(
                new YamlScalarNode(MotelyJokerSticker.Rental.ToString()) { Style = ScalarStyle.DoubleQuoted }
            );

        if (stickers.Children.Count > 0)
            map.Children[new YamlScalarNode("stickers")] = stickers;

        return map;
    }

    private static void AddAnteRange(YamlMappingNode map, int from, int toInclusive)
    {
        var seq = new YamlSequenceNode();
        for (int a = from; a <= toInclusive; a++)
            seq.Add(new YamlScalarNode(a.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        map.Children[new YamlScalarNode("antes")] = seq;
    }

    /// <summary>All booster slots that can appear when searching multiple antes (union).</summary>
    private static void AddBoosterPacksUnion(YamlMappingNode map) =>
        map.Children[new YamlScalarNode("boosterPacks")] = IntSequence([0, 1, 2, 3, 4, 5]);

    private static YamlSequenceNode IntSequence(int[] values)
    {
        var seq = new YamlSequenceNode();
        foreach (var v in values)
            seq.Add(new YamlScalarNode(v.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return seq;
    }

    private static YamlSequenceNode IntSequence(int single)
    {
        var seq = new YamlSequenceNode();
        seq.Add(new YamlScalarNode(single.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return seq;
    }
}
