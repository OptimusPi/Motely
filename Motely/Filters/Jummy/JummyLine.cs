using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Motely.Filters.Jaml;

namespace Motely.Filters.Jummy;

/// <summary>
/// JUMMY — one line, one JAML criterion.
///
/// The item half pivots through the engine's packed <see cref="MotelyItem"/> int:
/// <see cref="FormatUtils.FormatItem"/> projects the int to the exact descriptive
/// string the game / analyzer prints (e.g. <c>Eternal Negative Blueprint</c>), and
/// <see cref="FormatUtils.TryParseMotelyItem"/> parses it straight back to the same
/// int. Because the int is canonical, the round-trip is lossless and deterministic.
///
/// A JUMMY line is that item string plus an optional ante tail:
/// <code>
///   Eternal Blueprint in antes 1 or 2
/// </code>
/// which is the same criterion as the JAML clause:
/// <code>
///   - joker: Blueprint
///     stickers: [Eternal]
///     antes: [1, 2]
/// </code>
///
/// v0 covers single-card clauses across the packed-int families: jokers (with
/// edition + stickers) and the consumables tarot / spectral / planet. Standard
/// cards (rank/suit/enh/seal shape), vouchers, tags and bosses are separate enums
/// (not the packed int) and follow as incremental additions.
/// </summary>
public static class JummyLine
{
    private const string Wildcard = "Any";

    // ── Clause → line ─────────────────────────────────────────────────────────

    /// <summary>Renders a supported clause as a single JUMMY line, or null if unsupported by v0.</summary>
    public static string? FromClause(IJamlClause clause) =>
        clause switch
        {
            JokerClause j => FromJoker(j),
            TarotCardClause t => FromConsumable(t.Tarots, t.Antes),
            SpectralCardClause s => FromConsumable(s.Spectrals, s.Antes),
            PlanetCardClause p => FromConsumable(p.Planets, p.Antes),
            _ => null,
        };

    /// <summary>
    /// Consumable families (tarot / spectral / planet) carry no edition/sticker on the clause,
    /// so a single concrete card renders straight through the packed-int projection.
    /// </summary>
    private static string? FromConsumable<T>(T[] values, int[] antes)
        where T : struct, Enum
    {
        if (values.Length != 1)
            return null; // multi-card (OR) lists are not a single line in v0
        if (!Enum.TryParse<MotelyItemType>(values[0].ToString(), out var type))
            return null;
        return FormatUtils.FormatItem(new MotelyItem(type)) + AnteTail(antes);
    }

    private static string? FromJoker(JokerClause clause)
    {
        // v0: exactly one concrete joker, or the wildcard.
        string head;
        if (clause.IsWildcard)
        {
            head = Wildcard;
        }
        else if (clause.Jokers.Length == 1)
        {
            var item = new MotelyItem(clause.Jokers[0], clause.Edition ?? MotelyItemEdition.None);
            item = ApplyStickers(item, clause.Stickers);
            head = FormatUtils.FormatItem(item);
        }
        else
        {
            return null; // multi-joker (OR) lists are not a single line in v0
        }

        return head + AnteTail(clause.Antes);
    }

    private static MotelyItem ApplyStickers(MotelyItem item, MotelyJokerSticker[] stickers)
    {
        foreach (var sticker in stickers)
            item = sticker switch
            {
                MotelyJokerSticker.Eternal => item.WithEternal(true),
                MotelyJokerSticker.Perishable => item.WithPerishable(true),
                MotelyJokerSticker.Rental => item.WithRental(true),
                _ => item,
            };
        return item;
    }

    private static string AnteTail(int[] antes)
    {
        if (antes is not { Length: > 0 })
            return "";
        if (antes.Length == 1)
            return $" in ante {antes[0]}";
        return " in antes " + string.Join(" or ", antes);
    }

    // ── Line → clause ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a single JUMMY line into a clause. Returns false (with a reason) when the
    /// line names no recognizable item or the ante tail is malformed.
    /// </summary>
    public static bool TryToClause(string line, out IJamlClause? clause, out string? error)
    {
        clause = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Empty JUMMY line.";
            return false;
        }

        var (head, antes, tailError) = SplitTail(line.Trim());
        if (tailError != null)
        {
            error = tailError;
            return false;
        }

        // Wildcard joker: "Any [in antes …]"
        if (string.Equals(head, Wildcard, StringComparison.OrdinalIgnoreCase))
        {
            clause = new JokerClause { IsWildcard = true, Antes = antes };
            return true;
        }

        if (!FormatUtils.TryParseMotelyItem(head, out var item))
        {
            error = $"Unrecognized item: '{head}'.";
            return false;
        }

        switch (item.TypeCategory)
        {
            case MotelyItemTypeCategory.Joker when TryExtractJoker(item, out var joker):
                clause = new JokerClause
                {
                    Jokers = [joker],
                    Edition = item.Edition == MotelyItemEdition.None ? null : item.Edition,
                    Stickers = StickersOf(item),
                    Antes = antes,
                };
                return true;

            case MotelyItemTypeCategory.TarotCard when TryParseSpecific<MotelyTarotCard>(item, out var tarot):
                clause = new TarotCardClause { Tarots = [tarot], Antes = antes };
                return true;

            case MotelyItemTypeCategory.SpectralCard when TryParseSpecific<MotelySpectralCard>(item, out var spectral):
                clause = new SpectralCardClause { Spectrals = [spectral], Antes = antes };
                return true;

            case MotelyItemTypeCategory.PlanetCard when TryParseSpecific<MotelyPlanetCard>(item, out var planet):
                clause = new PlanetCardClause { Planets = [planet], Antes = antes };
                return true;
        }

        error = $"Item '{head}' isn't a JUMMY-supported clause yet (category {item.TypeCategory}).";
        return false;
    }

    /// <summary>
    /// Recovers a specific card enum from the packed item by name. The combined
    /// <see cref="MotelyItemType"/> shares member names with each category enum
    /// (e.g. <c>MotelyItemType.TheFool == TarotCard | MotelyTarotCard.TheFool</c>),
    /// so the type's name parses straight into the specific enum — no bit-math.
    /// </summary>
    private static bool TryParseSpecific<T>(MotelyItem item, out T value)
        where T : struct, Enum =>
        Enum.TryParse(item.Type.ToString(), ignoreCase: false, out value) && Enum.IsDefined(value);

    /// <summary>
    /// Recovers the <see cref="MotelyJoker"/> from the packed item via a one-time map keyed by the
    /// engine's own constructor output. The int is the source of truth; the lookup is O(1).
    /// </summary>
    private static readonly Dictionary<MotelyItemType, MotelyJoker> JokerByType =
        Enum.GetValues<MotelyJoker>().ToDictionary(j => new MotelyItem(j).Type);

    private static bool TryExtractJoker(MotelyItem item, out MotelyJoker joker) =>
        JokerByType.TryGetValue(item.Type, out joker);

    private static MotelyJokerSticker[] StickersOf(MotelyItem item)
    {
        var stickers = new List<MotelyJokerSticker>(3);
        if (item.IsEternal)
            stickers.Add(MotelyJokerSticker.Eternal);
        if (item.IsPerishable)
            stickers.Add(MotelyJokerSticker.Perishable);
        if (item.IsRental)
            stickers.Add(MotelyJokerSticker.Rental);
        return [.. stickers];
    }

    /// <summary>Splits "item in ante(s) …" into the item string and the ante list.</summary>
    private static (string Head, int[] Antes, string? Error) SplitTail(string line)
    {
        int idx = line.IndexOf(" in ante", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return (line, [], null);

        var head = line[..idx].TrimEnd();
        var tail = line[(idx + " in ante".Length)..];
        if (tail.StartsWith("s", StringComparison.OrdinalIgnoreCase))
            tail = tail[1..];

        var tokens = tail.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var antes = new List<int>(tokens.Length);
        foreach (var token in tokens)
        {
            if (token.Equals("or", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!int.TryParse(token, out var ante))
                return (head, [], $"Bad ante token '{token}' in '{line}'.");
            antes.Add(ante);
        }

        return (head, [.. antes], null);
    }
}
