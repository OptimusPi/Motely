using Motely.Filters;

namespace Motely.Analysis;

public static class MotelyJamlyzerHighlights
{
    public static SeedAnalysisDto Apply(JamlConfig config, SeedAnalysisDto analysis)
    {
        if (analysis.Error is not null)
            return analysis;

        var targets = EnumeratePreviewClauses(config)
            .Select(CreateJokerTarget)
            .Where(static target => target is not null)
            .Cast<JokerTarget>()
            .ToArray();

        if (targets.Length == 0)
            return analysis;

        analysis.Antes = analysis.Antes.Select(ante => HighlightAnte(ante, targets)).ToArray();
        return analysis;
    }

    private static AnteAnalysisDto HighlightAnte(
        AnteAnalysisDto ante,
        IReadOnlyList<JokerTarget> targets
    )
    {
        ante.ShopQueue = ante.ShopQueue
            .Select((item, slot) => item with
            {
                Matched = item.Matched || targets.Any(target =>
                    target.AppliesToAnte(ante.Ante)
                    && target.AppliesToShopSlot(slot)
                    && target.Matches(new MotelyItem(item.Value))),
            })
            .ToArray();

        ante.Packs = ante.Packs
            .Select((pack, slot) => pack with
            {
                Items = pack.Items.Select(item => item with
                {
                    Matched = item.Matched || targets.Any(target =>
                        target.AppliesToAnte(ante.Ante)
                        && target.AppliesToBoosterSlot(slot)
                        && target.Matches(new MotelyItem(item.Value))),
                }).ToArray(),
            })
            .ToArray();

        return ante;
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

    private static JokerTarget? CreateJokerTarget(IJamlClause clause) =>
        clause switch
        {
            JokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item => MatchesJoker(item, c.IsWildcard, c.Jokers.Select(static j => j.ToString()), c.Edition, c.Stickers, null)),
            CommonJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item => MatchesJoker(item, c.IsWildcard, c.Jokers.Select(static j => j.ToString()), c.Edition, c.Stickers,
                    static item => Enum.TryParse<MotelyJokerCommon>(item.Type.ToString(), out _))),
            UncommonJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item => MatchesJoker(item, c.IsWildcard, c.Jokers.Select(static j => j.ToString()), c.Edition, c.Stickers,
                    static item => Enum.TryParse<MotelyJokerUncommon>(item.Type.ToString(), out _))),
            RareJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item => MatchesJoker(item, c.IsWildcard, c.Jokers.Select(static j => j.ToString()), c.Edition, c.Stickers,
                    static item => Enum.TryParse<MotelyJokerRare>(item.Type.ToString(), out _))),
            MixedJokerClause c => new(
                c.Antes,
                c.Sources.ShopItems,
                c.Sources.BoosterPacks,
                item => MatchesJoker(item, c.IsWildcard, c.Jokers.Select(static j => j.ToString()), c.Edition, c.Stickers, null)),
            _ => null,
        };

    private static bool MatchesJoker(
        MotelyItem item,
        bool isWildcard,
        IEnumerable<string> jokerNames,
        MotelyItemEdition? edition,
        IReadOnlyList<MotelyJokerSticker> stickers,
        Func<MotelyItem, bool>? rarityMatch
    )
    {
        if (item.TypeCategory != MotelyItemTypeCategory.Joker)
            return false;
        if (rarityMatch is not null && !rarityMatch(item))
            return false;
        if (!isWildcard && !jokerNames.Contains(item.Type.ToString()))
            return false;
        if (edition.HasValue && item.Edition != edition.Value)
            return false;

        return stickers.All(sticker => sticker switch
        {
            MotelyJokerSticker.Eternal => item.IsEternal,
            MotelyJokerSticker.Perishable => item.IsPerishable,
            MotelyJokerSticker.Rental => item.IsRental,
            _ => true,
        });
    }

    private sealed record JokerTarget(
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
