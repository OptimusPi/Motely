using System;
using System.Collections.Generic;
using System.Linq;
using Motely.Filters.Jaml;

namespace Motely.Filters.Jummy;

/// <summary>
/// JUMMY — one line, one JAML criterion. Formatting/parsing delegates item and enum
/// spellings to the engine's own canonical formatters whenever they exist.
/// </summary>
public static class JummyLine
{
    private const string Wildcard = "Any";
    private const string VoucherPrefix = "Voucher ";
    private const string TagPrefix = "Tag ";
    private const string SmallBlindTagPrefix = "Small Blind Tag ";
    private const string BigBlindTagPrefix = "Big Blind Tag ";
    private const string BossPrefix = "Boss ";
    private const string StartingDrawPrefix = "Starting Draw ";

    // ── Clause → line ─────────────────────────────────────────────────────────

    public static string? FromClause(IJamlClause clause) =>
        clause switch
        {
            JokerClause j => FromJoker(j),
            TarotCardClause t => FromConsumable(t.Tarots, t.Antes),
            SpectralCardClause s => FromConsumable(s.Spectrals, s.Antes),
            PlanetCardClause p => FromConsumable(p.Planets, p.Antes),
            StandardCardClause s => FromStandardCard(s),
            VoucherClause v => FromVoucher(v),
            TagClause t => FromTag(t),
            BossClause b => FromBoss(b),
            StartingDrawClause s => FromStartingDraw(s),
            LuckyMoneyClause e => FromRollEvent("Lucky Money", e.Rolls, e.With),
            LuckyMultClause e => FromRollEvent("Lucky Mult", e.Rolls, e.With),
            MisprintMultClause e => FromMisprint(e),
            WheelOfFortuneClause e => FromRollEvent("Wheel of Fortune", e.Rolls, e.With),
            GrosMichelExtinctClause e => FromRollEvent("Gros Michel Extinct", e.Rolls, e.With),
            CavendishExtinctClause e => FromRollEvent("Cavendish Extinct", e.Rolls, e.With),
            SpaceLevelupClause e => FromRollEvent("Space Levelup", e.Rolls, e.With),
            GlassDestroyClause e => FromRollEvent("Glass Destroy", e.Rolls, e.With),
            WheelStaysFlippedClause e => FromRollEvent("Wheel Stays Flipped", e.Rolls, e.With),
            BusinessPayoutClause e => FromRollEvent("Business Payout", e.Rolls, null),
            BloodstoneTriggerClause e => FromRollEvent("Bloodstone Trigger", e.Rolls, null),
            ParkingPayoutClause e => FromRollEvent("Parking Payout", e.Rolls, null),
            _ => null,
        };

    private static string? FromConsumable<T>(T[] values, int[] antes)
        where T : struct, Enum
    {
        if (values.Length != 1)
            return null;
        if (!Enum.TryParse<MotelyItemType>(values[0].ToString(), out var type))
            return null;
        return FormatUtils.FormatItem(new MotelyItem(type)) + AnteTail(antes);
    }

    private static string? FromJoker(JokerClause clause)
    {
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
            return null;
        }

        return head + AnteTail(clause.Antes);
    }

    private static string? FromStandardCard(StandardCardClause clause)
    {
        if (clause.Rank is not { } rank || clause.Suit is not { } suit)
            return null;

        var card = (MotelyStandardCard)((int)rank | (int)suit);
        var item = new MotelyItem(card);
        if (clause.Seal is { } seal)
            item = item.WithSeal(seal);
        if (clause.Edition is { } edition)
            item = item.WithEdition(edition);
        if (clause.Enhancement is { } enhancement)
            item = item.WithEnhancement(enhancement);
        return FormatUtils.FormatItem(item) + AnteTail(clause.Antes);
    }

    private static string? FromStartingDraw(StartingDrawClause clause)
    {
        if (clause.Rank is not { } rank || clause.Suit is not { } suit)
            return null;
        var card = (MotelyStandardCard)((int)rank | (int)suit);
        return StartingDrawPrefix
            + FormatUtils.FormatItem(new MotelyItem(card))
            + AnteTail(clause.Antes);
    }

    private static string? FromVoucher(VoucherClause clause)
    {
        if (clause.Vouchers.Length != 1)
            return null;
        return VoucherPrefix
            + FormatUtils.FormatVoucher(clause.Vouchers[0])
            + RollsTail(clause.Rolls)
            + AnteTail(clause.Antes);
    }

    private static string? FromTag(TagClause clause)
    {
        if (clause.Tags.Length != 1)
            return null;
        var prefix = clause.Rolls switch
        {
            [0] => SmallBlindTagPrefix,
            [1] => BigBlindTagPrefix,
            _ => TagPrefix,
        };
        var rolls = prefix == TagPrefix ? RollsTail(clause.Rolls) : "";
        return prefix + FormatUtils.FormatTag(clause.Tags[0]) + rolls + AnteTail(clause.Antes);
    }

    private static string? FromBoss(BossClause clause)
    {
        if (clause.Bosses.Length != 1)
            return null;
        return BossPrefix + FormatUtils.FormatBoss(clause.Bosses[0]) + AnteTail(clause.Antes);
    }

    private static string FromMisprint(MisprintMultClause clause) =>
        "Misprint Mult" + RollsTail(clause.Rolls) + $" mult {clause.Mult}";

    private static string FromRollEvent(string name, int[] rolls, JamlWith? with)
    {
        var line = name + RollsTail(rolls);
        if (with?.Luck is { } luck && luck != MotelyLuck.X1)
            line += $" with luck {(int)luck}";
        return line;
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

    private static string RollsTail(int[] rolls)
    {
        if (rolls is not { Length: > 0 })
            return "";
        return " rolls " + JoinNumbers(rolls);
    }

    private static string AnteTail(int[] antes)
    {
        if (antes is not { Length: > 0 })
            return "";
        if (antes.Length == 1)
            return $" in ante {antes[0]}";
        return " in antes " + JoinNumbers(antes);
    }

    private static string JoinNumbers(int[] values) => string.Join(" or ", values);

    // ── Line → clause ─────────────────────────────────────────────────────────

    public static bool TryToClause(string line, out IJamlClause? clause, out string? error)
    {
        clause = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Empty JUMMY line.";
            return false;
        }

        var (withoutAnte, antes, anteError) = SplitAnteTail(line.Trim());
        if (anteError != null)
        {
            error = anteError;
            return false;
        }

        if (TryParsePrefixed(withoutAnte, StartingDrawPrefix, out var startingText))
            return TryParseStartingDraw(startingText, antes, out clause, out error);
        if (TryParsePrefixed(withoutAnte, SmallBlindTagPrefix, out var smallTagText))
            return TryParseTag(smallTagText, [0], antes, out clause, out error);
        if (TryParsePrefixed(withoutAnte, BigBlindTagPrefix, out var bigTagText))
            return TryParseTag(bigTagText, [1], antes, out clause, out error);
        if (TryParsePrefixed(withoutAnte, TagPrefix, out var tagText))
            return TryParseGenericTag(tagText, antes, out clause, out error);
        if (TryParsePrefixed(withoutAnte, VoucherPrefix, out var voucherText))
            return TryParseVoucher(voucherText, antes, out clause, out error);
        if (TryParsePrefixed(withoutAnte, BossPrefix, out var bossText))
            return TryParseBoss(bossText, antes, out clause, out error);
        if (TryParseEvent(withoutAnte, out clause, out error))
            return true;
        if (error != null)
            return false;

        if (string.Equals(withoutAnte, Wildcard, StringComparison.OrdinalIgnoreCase))
        {
            clause = new JokerClause { IsWildcard = true, Antes = antes };
            return true;
        }

        if (!FormatUtils.TryParseMotelyItem(withoutAnte, out var item))
        {
            error = $"Unrecognized item: '{withoutAnte}'.";
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

            case MotelyItemTypeCategory.TarotCard
                when TryParseSpecific<MotelyTarotCard>(item, out var tarot):
                clause = new TarotCardClause { Tarots = [tarot], Antes = antes };
                return true;

            case MotelyItemTypeCategory.SpectralCard
                when TryParseSpecific<MotelySpectralCard>(item, out var spectral):
                clause = new SpectralCardClause { Spectrals = [spectral], Antes = antes };
                return true;

            case MotelyItemTypeCategory.PlanetCard
                when TryParseSpecific<MotelyPlanetCard>(item, out var planet):
                clause = new PlanetCardClause { Planets = [planet], Antes = antes };
                return true;

            case MotelyItemTypeCategory.Standardcard:
                clause = new StandardCardClause
                {
                    Rank = item.StandardcardRank,
                    Suit = item.StandardcardSuit,
                    Seal = item.Seal == MotelyItemSeal.None ? null : item.Seal,
                    Edition = item.Edition == MotelyItemEdition.None ? null : item.Edition,
                    Enhancement =
                        item.Enhancement == MotelyItemEnhancement.None ? null : item.Enhancement,
                    Antes = antes,
                };
                return true;
        }

        error =
            $"Item '{withoutAnte}' isn't a JUMMY-supported clause yet (category {item.TypeCategory}).";
        return false;
    }

    private static bool TryParseStartingDraw(
        string text,
        int[] antes,
        out IJamlClause? clause,
        out string? error
    )
    {
        clause = null;
        if (
            !FormatUtils.TryParseMotelyItem(text, out var item)
            || item.TypeCategory != MotelyItemTypeCategory.Standardcard
        )
        {
            error = $"Starting Draw requires a standard card, got '{text}'.";
            return false;
        }

        if (
            item.Seal != MotelyItemSeal.None
            || item.Edition != MotelyItemEdition.None
            || item.Enhancement != MotelyItemEnhancement.None
        )
        {
            error = "Starting Draw supports rank/suit only.";
            return false;
        }

        clause = new StartingDrawClause
        {
            Rank = item.StandardcardRank,
            Suit = item.StandardcardSuit,
            Antes = antes,
        };
        error = null;
        return true;
    }

    private static bool TryParseVoucher(
        string text,
        int[] antes,
        out IJamlClause? clause,
        out string? error
    )
    {
        var (head, rolls, rollError) = SplitRollsTail(text);
        if (rollError != null)
        {
            clause = null;
            error = rollError;
            return false;
        }

        if (!TryParseFormattedEnum(head, FormatUtils.FormatVoucher, out MotelyVoucher voucher))
        {
            clause = null;
            error = $"Unrecognized voucher: '{head}'.";
            return false;
        }

        clause = new VoucherClause
        {
            Vouchers = [voucher],
            Rolls = rolls.Length == 0 ? [0] : rolls,
            Antes = antes,
        };
        error = null;
        return true;
    }

    private static bool TryParseGenericTag(
        string text,
        int[] antes,
        out IJamlClause? clause,
        out string? error
    )
    {
        var (head, rolls, rollError) = SplitRollsTail(text);
        if (rollError != null)
        {
            clause = null;
            error = rollError;
            return false;
        }
        return TryParseTag(head, rolls.Length == 0 ? [0, 1] : rolls, antes, out clause, out error);
    }

    private static bool TryParseTag(
        string text,
        int[] rolls,
        int[] antes,
        out IJamlClause? clause,
        out string? error
    )
    {
        if (!TryParseFormattedEnum(text, FormatUtils.FormatTag, out MotelyTag tag))
        {
            clause = null;
            error = $"Unrecognized tag: '{text}'.";
            return false;
        }

        clause = new TagClause
        {
            Tags = [tag],
            Rolls = rolls,
            Antes = antes,
        };
        error = null;
        return true;
    }

    private static bool TryParseBoss(
        string text,
        int[] antes,
        out IJamlClause? clause,
        out string? error
    )
    {
        if (!TryParseFormattedEnum(text, FormatUtils.FormatBoss, out MotelyBossBlind boss))
        {
            clause = null;
            error = $"Unrecognized boss: '{text}'.";
            return false;
        }

        clause = new BossClause { Bosses = [boss], Antes = antes };
        error = null;
        return true;
    }

    private static bool TryParseEvent(string text, out IJamlClause? clause, out string? error)
    {
        clause = null;
        error = null;

        if (TryParseEventPayload(text, "Misprint Mult", out var misprintPayload))
        {
            var (withoutMult, mult, multError) = SplitRequiredIntTail(misprintPayload, " mult ");
            if (multError != null)
            {
                error = multError;
                return false;
            }
            var (rolls, rollError) = ParseRequiredRolls(withoutMult, "Misprint Mult");
            if (rollError != null)
            {
                error = rollError;
                return false;
            }
            clause = new MisprintMultClause { Rolls = rolls, Mult = mult };
            return true;
        }

        foreach (var spec in EventSpecs)
        {
            if (!TryParseEventPayload(text, spec.Name, out var payload))
                continue;

            var (withoutLuck, luck, luckError) = SplitLuckTail(payload, spec.AllowLuck);
            if (luckError != null)
            {
                error = luckError;
                return false;
            }

            var (rolls, rollError) = ParseRequiredRolls(withoutLuck, spec.Name);
            if (rollError != null)
            {
                error = rollError;
                return false;
            }

            var with = new JamlWith { Luck = luck ?? MotelyLuck.X1 };
            clause = spec.Create(rolls, with);
            return true;
        }

        return false;
    }

    private static readonly EventSpec[] EventSpecs =
    [
        new(
            "Lucky Money",
            true,
            static (rolls, with) => new LuckyMoneyClause { Rolls = rolls, With = with }
        ),
        new(
            "Lucky Mult",
            true,
            static (rolls, with) => new LuckyMultClause { Rolls = rolls, With = with }
        ),
        new(
            "Wheel of Fortune",
            true,
            static (rolls, with) => new WheelOfFortuneClause { Rolls = rolls, With = with }
        ),
        new(
            "Gros Michel Extinct",
            true,
            static (rolls, with) => new GrosMichelExtinctClause { Rolls = rolls, With = with }
        ),
        new(
            "Cavendish Extinct",
            true,
            static (rolls, with) => new CavendishExtinctClause { Rolls = rolls, With = with }
        ),
        new(
            "Space Levelup",
            true,
            static (rolls, with) => new SpaceLevelupClause { Rolls = rolls, With = with }
        ),
        new(
            "Glass Destroy",
            true,
            static (rolls, with) => new GlassDestroyClause { Rolls = rolls, With = with }
        ),
        new(
            "Wheel Stays Flipped",
            true,
            static (rolls, with) => new WheelStaysFlippedClause { Rolls = rolls, With = with }
        ),
        new(
            "Business Payout",
            false,
            static (rolls, _) => new BusinessPayoutClause { Rolls = rolls }
        ),
        new(
            "Bloodstone Trigger",
            false,
            static (rolls, _) => new BloodstoneTriggerClause { Rolls = rolls }
        ),
        new(
            "Parking Payout",
            false,
            static (rolls, _) => new ParkingPayoutClause { Rolls = rolls }
        ),
    ];

    private readonly record struct EventSpec(
        string Name,
        bool AllowLuck,
        Func<int[], JamlWith, IJamlClause> Create
    );

    private static bool TryParseEventPayload(string text, string name, out string payload)
    {
        payload = "";
        if (!text.StartsWith(name, StringComparison.OrdinalIgnoreCase))
            return false;
        if (text.Length > name.Length && text[name.Length] != ' ')
            return false;
        payload = text[name.Length..].TrimStart();
        return true;
    }

    private static (string Text, MotelyLuck? Luck, string? Error) SplitLuckTail(
        string text,
        bool allowLuck
    )
    {
        const string marker = " with luck ";
        int idx = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return (text, null, null);
        if (!allowLuck)
            return (text, null, "This JUMMY event does not support luck.");
        var luckText = text[(idx + marker.Length)..].Trim();
        if (!int.TryParse(luckText, out var numeric) || !TryParseLuck(numeric, out var luck))
            return (text, null, $"Bad luck multiplier '{luckText}'.");
        return (text[..idx].TrimEnd(), luck, null);
    }

    private static (string Text, int Value, string? Error) SplitRequiredIntTail(
        string text,
        string marker
    )
    {
        int idx = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return (text, 0, $"Missing required '{marker.Trim()}' tail.");
        var valueText = text[(idx + marker.Length)..].Trim();
        if (!int.TryParse(valueText, out var value))
            return (text, 0, $"Bad integer '{valueText}' in '{text}'.");
        return (text[..idx].TrimEnd(), value, null);
    }

    private static (int[] Rolls, string? Error) ParseRequiredRolls(string text, string name)
    {
        var (head, rolls, error) = SplitRollsTail(text);
        if (error != null)
            return ([], error);
        if (!string.IsNullOrWhiteSpace(head))
            return ([], $"Unexpected text after '{name}': '{head}'.");
        if (rolls.Length == 0)
            return ([], $"{name} requires a rolls tail.");
        return (rolls, null);
    }

    private static bool TryParseLuck(int value, out MotelyLuck luck)
    {
        foreach (var candidate in Enum.GetValues<MotelyLuck>())
        {
            if ((int)candidate == value)
            {
                luck = candidate;
                return true;
            }
        }
        luck = default;
        return false;
    }

    private static bool TryParsePrefixed(string text, string prefix, out string rest)
    {
        if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            rest = text[prefix.Length..].Trim();
            return true;
        }
        rest = "";
        return false;
    }

    private static bool TryParseFormattedEnum<T>(string text, Func<T, string> format, out T value)
        where T : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<T>())
        {
            if (
                string.Equals(format(candidate), text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.ToString(), text, StringComparison.OrdinalIgnoreCase)
            )
            {
                value = candidate;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool TryParseSpecific<T>(MotelyItem item, out T value)
        where T : struct, Enum =>
        Enum.TryParse(item.Type.ToString(), ignoreCase: false, out value) && Enum.IsDefined(value);

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

    private static (string Head, int[] Rolls, string? Error) SplitRollsTail(string text)
    {
        const string marker = "rolls ";
        int idx = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0 || (idx > 0 && !char.IsWhiteSpace(text[idx - 1])))
            return (text.Trim(), [], null);
        var head = text[..idx].TrimEnd();
        var tail = text[(idx + marker.Length)..];
        var (values, error) = ParseNumberList(tail, text);
        return (head, values, error);
    }

    private static (string Head, int[] Antes, string? Error) SplitAnteTail(string line)
    {
        int idx = line.IndexOf(" in ante", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return (line, [], null);

        var head = line[..idx].TrimEnd();
        var tail = line[(idx + " in ante".Length)..];
        if (tail.StartsWith("s", StringComparison.OrdinalIgnoreCase))
            tail = tail[1..];

        var (antes, error) = ParseNumberList(tail, line);
        return (head, antes, error);
    }

    private static (int[] Values, string? Error) ParseNumberList(string text, string fullLine)
    {
        var tokens = text.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var values = new List<int>(tokens.Length);
        foreach (var token in tokens)
        {
            if (token.Equals("or", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!int.TryParse(token, out var value))
                return ([], $"Bad numeric token '{token}' in '{fullLine}'.");
            values.Add(value);
        }
        return ([.. values], null);
    }
}
