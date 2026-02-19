using System;
using System.Text;

namespace Motely.Filters;

/// <summary>
/// Generates human-readable labels for JAML clauses. AOT-safe static switch, no reflection.
/// </summary>
internal static class JamlClauseLabeler
{
    internal static string Generate(MotelyFilterItemType itemType, JamlClauseDto c, int[] antes, int min)
    {
        string antesStr = antes.Length == 0 ? "" : antes.Length == 1 ? $" A{antes[0]}" : $" A{string.Join(",", antes)}";
        string minStr = min > 1 ? $" (min {min})" : "";

        return itemType switch
        {
            MotelyFilterItemType.Joker or
            MotelyFilterItemType.CommonJoker or
            MotelyFilterItemType.UncommonJoker or
            MotelyFilterItemType.RareJoker =>
                FormatList(c.Jokers, c.Joker, antesStr, minStr),

            MotelyFilterItemType.SoulJoker =>
                FormatList(c.Jokers, c.SoulJoker, antesStr, minStr, "Legendary "),

            MotelyFilterItemType.Voucher =>
                FormatList(c.Vouchers, c.Voucher, antesStr, minStr),

            MotelyFilterItemType.TarotCard =>
                FormatSingle(c.Tarot ?? c.TarotCard, antesStr, minStr),

            MotelyFilterItemType.SpectralCard =>
                FormatSingle(c.Spectral ?? c.SpectralCard, antesStr, minStr),

            MotelyFilterItemType.PlanetCard =>
                FormatSingle(c.Planet ?? c.PlanetCard, antesStr, minStr),

            MotelyFilterItemType.Boss =>
                FormatSingle(c.Boss, antesStr, minStr, "Boss: "),

            MotelyFilterItemType.SmallBlindTag =>
                FormatSingle(c.SmallBlindTag ?? c.Tag, antesStr, minStr, "SmallBlind Tag: "),

            MotelyFilterItemType.BigBlindTag =>
                FormatSingle(c.BigBlindTag, antesStr, minStr, "BigBlind Tag: "),

            MotelyFilterItemType.PlayingCard =>
                FormatCard(c.Rank, c.Suit, "Standard Card", antesStr, minStr),

            MotelyFilterItemType.ErraticRank =>
                $"Erratic Rank: {c.Rank ?? c.ErraticRank ?? "Any"}{antesStr}{minStr}",

            MotelyFilterItemType.ErraticSuit =>
                $"Erratic Suit: {c.Suit ?? c.ErraticSuit ?? "Any"}{antesStr}{minStr}",

            MotelyFilterItemType.ErraticCard =>
                FormatCard(c.Rank, c.Suit, "Erratic Card", antesStr, minStr),

            MotelyFilterItemType.StartingDraw =>
                FormatCard(c.Rank, c.Suit, "Starting Draw", antesStr, minStr),

            MotelyFilterItemType.Event =>
                $"{c.EventType ?? c.Event ?? "Event"}{antesStr}{minStr}",

            _ => itemType.ToString() + antesStr + minStr,
        };
    }

    private static string FormatList(System.Collections.Generic.List<string>? list, string? single, string antesStr, string minStr, string prefix = "")
    {
        if (list != null && list.Count > 0)
            return prefix + string.Join("/", list) + antesStr + minStr;
        if (single != null)
            return prefix + single + antesStr + minStr;
        return prefix + "?" + antesStr + minStr;
    }

    private static string FormatSingle(string? value, string antesStr, string minStr, string prefix = "")
    {
        return prefix + (value ?? "?") + antesStr + minStr;
    }

    private static string FormatCard(string? rank, string? suit, string kind, string antesStr, string minStr)
    {
        if (rank != null && suit != null) return $"{kind}: {rank} of {suit}{antesStr}{minStr}";
        if (rank != null) return $"{kind}: {rank}{antesStr}{minStr}";
        if (suit != null) return $"{kind}: {suit}{antesStr}{minStr}";
        return $"{kind}{antesStr}{minStr}";
    }
}
