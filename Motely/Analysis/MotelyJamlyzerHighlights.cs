using System;
using System.Collections.Generic;
using System.Linq;
using Motely.Enums;
using Motely.Filters;
using Motely.Filters.Jaml;

namespace Motely.Analysis;

public static class MotelyJamlyzerHighlights
{
    public static MotelySeedAnalysis Apply(JamlConfig config, MotelySeedAnalysis analysis)
    {
        if (analysis.Error is not null)
            return analysis;

        var clauses = EnumeratePreviewClauses(config).ToArray();

        var targets = clauses
            .Select(CreateHighlightTarget)
            .Where(static target => target is not null)
            .Cast<HighlightTarget>()
            .ToArray();

        if (clauses.Length == 0)
            return analysis;

        return analysis with
        {
            Antes = analysis.Antes.Select(ante => HighlightAnte(ante, targets, clauses)).ToList(),
        };
    }

    private static MotelyAnteAnalysis HighlightAnte(
        MotelyAnteAnalysis ante,
        IReadOnlyList<HighlightTarget> targets,
        IReadOnlyList<IJamlClause> clauses
    )
    {
        var newShopQueue = ante
            .ShopQueue.Select(
                (item, slot) =>
                    item with
                    {
                        Matched =
                            item.Matched
                            || targets.Any(target =>
                                target.AppliesToAnte(ante.Ante)
                                && target.AppliesToShopSlot(slot)
                                && target.Matches(item.Item)
                            ),
                    }
            )
            .ToList();

        var newPacks = ante
            .Packs.Select(
                (pack, slot) =>
                    pack with
                    {
                        Items = pack
                            .Items.Select(packItem =>
                                packItem with
                                {
                                    Matched =
                                        packItem.Matched
                                        || targets.Any(target =>
                                            target.AppliesToAnte(ante.Ante)
                                            && target.AppliesToBoosterSlot(slot)
                                            && target.Matches(packItem.Item)
                                        ),
                                }
                            )
                            .ToList(),
                    }
            )
            .ToList();

        bool bossMatched = clauses
            .OfType<BossClause>()
            .Any(c => c.Antes.Contains(ante.Ante) && c.Bosses.Contains(ante.Boss));

        bool voucherMatched = clauses
            .OfType<VoucherClause>()
            .Any(c =>
                c.Antes.Contains(ante.Ante)
                && c.Rolls.Contains(0)
                && c.Vouchers.Contains(ante.Voucher)
            );

        bool smallBlindTagMatched = clauses
            .OfType<TagClause>()
            .Any(c =>
                c.Antes.Contains(ante.Ante)
                && c.Rolls.Contains(0)
                && c.Tags.Contains(ante.SmallBlindTag)
            );

        bool bigBlindTagMatched = clauses
            .OfType<TagClause>()
            .Any(c =>
                c.Antes.Contains(ante.Ante)
                && c.Rolls.Contains(1)
                && c.Tags.Contains(ante.BigBlindTag)
            );

        return ante with
        {
            ShopQueue = newShopQueue,
            Packs = newPacks,
            BossMatched = ante.BossMatched || bossMatched,
            VoucherMatched = ante.VoucherMatched || voucherMatched,
            SmallBlindTagMatched = ante.SmallBlindTagMatched || smallBlindTagMatched,
            BigBlindTagMatched = ante.BigBlindTagMatched || bigBlindTagMatched,
        };
    }

    private static IEnumerable<IJamlClause> EnumeratePreviewClauses(JamlConfig config)
    {
        foreach (var clause in EnumerateClauseSet(config.Must))
            yield return clause;
        foreach (var clause in EnumerateClauseSet(config.Should))
            yield return clause;
    }

    private static IEnumerable<IJamlClause> EnumerateClauseSet(JamlClauseSet set)
    {
        foreach (var clause in set.OrderedClauses)
        {
            foreach (var flattened in FlattenClause(clause))
                yield return flattened;
        }
    }

    private static IEnumerable<IJamlClause> FlattenClause(IJamlClause clause)
    {
        switch (clause)
        {
            case AndClause and:
                foreach (var child in and.Clauses.SelectMany(FlattenClause))
                    yield return child;
                break;
            case OrClause or:
                foreach (var child in or.Clauses.SelectMany(FlattenClause))
                    yield return child;
                break;
            default:
                yield return clause;
                break;
        }
    }

    private static HighlightTarget? CreateHighlightTarget(IJamlClause clause) =>
        clause switch
        {
            JokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item => MatchesJoker(item, c.IsWildcard, c.Jokers, c.Edition, c.Stickers, null)
            ),
            CommonJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item =>
                    MatchesJoker(
                        item,
                        c.IsWildcard,
                        c.Jokers.Select(static j =>
                                (MotelyJoker)((int)MotelyJokerRarity.Common | (int)j)
                            )
                            .ToArray(),
                        c.Edition,
                        c.Stickers,
                        static item =>
                            (MotelyJokerRarity)(item.Value & MotelyGlobals.JokerRarityMask)
                            == MotelyJokerRarity.Common
                    )
            ),
            UncommonJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item =>
                    MatchesJoker(
                        item,
                        c.IsWildcard,
                        c.Jokers.Select(static j =>
                                (MotelyJoker)((int)MotelyJokerRarity.Uncommon | (int)j)
                            )
                            .ToArray(),
                        c.Edition,
                        c.Stickers,
                        static item =>
                            (MotelyJokerRarity)(item.Value & MotelyGlobals.JokerRarityMask)
                            == MotelyJokerRarity.Uncommon
                    )
            ),
            RareJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item =>
                    MatchesJoker(
                        item,
                        c.IsWildcard,
                        c.Jokers.Select(static j =>
                                (MotelyJoker)((int)MotelyJokerRarity.Rare | (int)j)
                            )
                            .ToArray(),
                        c.Edition,
                        c.Stickers,
                        static item =>
                            (MotelyJokerRarity)(item.Value & MotelyGlobals.JokerRarityMask)
                            == MotelyJokerRarity.Rare
                    )
            ),
            MixedJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item => MatchesJoker(item, c.IsWildcard, c.Jokers, c.Edition, c.Stickers, null)
            ),
            LegendaryJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item =>
                {
                    if (
                        item.TypeCategory == MotelyItemTypeCategory.SpectralCard
                        && item.Type == MotelyItemType.TheSoul
                    )
                        return true;
                    if (item.TypeCategory != MotelyItemTypeCategory.Joker)
                        return false;
                    bool typeMatches =
                        c.IsWildcard
                        || c.Jokers.Any(j =>
                            item.Type
                            == (MotelyItemType)(
                                (int)MotelyItemTypeCategory.Joker
                                | (int)MotelyJokerRarity.Legendary
                                | (int)j
                            )
                        );
                    if (!typeMatches)
                        return false;
                    return !c.Edition.HasValue || item.Edition == c.Edition.Value;
                }
            ),
            TarotCardClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item =>
                    item.TypeCategory == MotelyItemTypeCategory.TarotCard
                    && c.Tarots.Any(t =>
                        item.Type
                        == (MotelyItemType)((int)MotelyItemTypeCategory.TarotCard | (int)t)
                    )
            ),
            PlanetCardClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item =>
                    item.TypeCategory == MotelyItemTypeCategory.PlanetCard
                    && c.Planets.Any(p =>
                        item.Type
                        == (MotelyItemType)((int)MotelyItemTypeCategory.PlanetCard | (int)p)
                    )
            ),
            SpectralCardClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item =>
                    item.TypeCategory == MotelyItemTypeCategory.SpectralCard
                    && c.Spectrals.Any(s =>
                        item.Type
                        == (MotelyItemType)((int)MotelyItemTypeCategory.SpectralCard | (int)s)
                    )
            ),
            VoucherClause c => new(c.Antes, [], [], item => false),
            TagClause c => new(c.Antes, [], [], item => false),
            BossClause c => new(c.Antes, [], [], item => false),
            StandardCardClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item =>
                {
                    if (item.TypeCategory != MotelyItemTypeCategory.Standardcard)
                        return false;
                    if (c.Rank.HasValue && item.StandardcardRank != c.Rank.Value)
                        return false;
                    if (c.Suit.HasValue && item.StandardcardSuit != c.Suit.Value)
                        return false;
                    if (c.Enhancement.HasValue && item.Enhancement != c.Enhancement.Value)
                        return false;
                    if (c.Seal.HasValue && item.Seal != c.Seal.Value)
                        return false;
                    if (c.Edition.HasValue && item.Edition != c.Edition.Value)
                        return false;
                    return true;
                }
            ),
            _ => null,
        };

    private static bool MatchesJoker(
        MotelyItem item,
        bool isWildcard,
        IReadOnlyList<MotelyJoker> jokers,
        MotelyItemEdition? edition,
        IReadOnlyList<MotelyJokerSticker> stickers,
        Func<MotelyItem, bool>? rarityMatch
    )
    {
        if (item.TypeCategory != MotelyItemTypeCategory.Joker)
            return false;
        if (rarityMatch is not null && !rarityMatch(item))
            return false;
        if (!isWildcard)
        {
            bool found = false;
            for (int i = 0; i < jokers.Count; i++)
            {
                if (
                    item.Type
                    == (MotelyItemType)((int)MotelyItemTypeCategory.Joker | (int)jokers[i])
                )
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                return false;
        }
        if (edition.HasValue && item.Edition != edition.Value)
            return false;

        return stickers.All(sticker =>
            sticker switch
            {
                MotelyJokerSticker.Eternal => item.IsEternal,
                MotelyJokerSticker.Perishable => item.IsPerishable,
                MotelyJokerSticker.Rental => item.IsRental,
                _ => true,
            }
        );
    }

    private sealed record HighlightTarget(
        int[] Antes,
        int[] ShopItems,
        int[] BoosterSlots,
        Func<MotelyItem, bool> Matches
    )
    {
        public bool AppliesToAnte(int ante) => Antes.Contains(ante);

        public bool AppliesToShopSlot(int slot) => ShopItems.Contains(slot);

        public bool AppliesToBoosterSlot(int slot) => BoosterSlots.Contains(slot);
    }
}
