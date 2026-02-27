using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely.Filters;

public sealed class StandardCardClause : IJamlClause
{
    public string Label { get; init; } = "";
    public int Score { get; init; }
    public MotelyPlayingCardRank? Rank { get; init; }
    public MotelyPlayingCardSuit? Suit { get; init; }
    public MotelyItemEnhancement? Enhancement { get; init; }
    public MotelyItemSeal? Seal { get; init; }
    public MotelyItemEdition? Edition { get; init; }
    public StandardCardSourceConfig Sources { get; init; } = new();
    public int[] Antes { get; init; } = [];
    public int Min { get; init; } = 1;
}

public struct StandardCardFilterDesc(StandardCardClause clause)
    : IMotelySeedFilterDesc<StandardCardFilterDesc.StandardCardFilter>
{
    private readonly StandardCardClause _clause = clause;

    public StandardCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var ante in _clause.Antes)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        int maxShopItem = 0;
        for (int i = 0; i < _clause.Sources.ShopItems.Length; i++)
        {
            if (_clause.Sources.ShopItems[i] > maxShopItem)
                maxShopItem = _clause.Sources.ShopItems[i];
        }

        int maxBoosterPack = 0;
        for (int i = 0; i < _clause.Sources.BoosterPacks.Length; i++)
        {
            if (_clause.Sources.BoosterPacks[i] > maxBoosterPack)
                maxBoosterPack = _clause.Sources.BoosterPacks[i];
        }

        return new StandardCardFilter(_clause, maxShopItem, maxBoosterPack);
    }

    public struct StandardCardFilter(StandardCardClause clause, int maxShopItem, int maxBoosterPack)
        : IMotelySeedFilter
    {
        private readonly StandardCardClause _clause = clause;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;

        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            var clause = _clause;
            int maxShopItem = _maxShopItem;
            int maxBoosterPack = _maxBoosterPack;

            return ctx.SearchIndividualSeeds(
                (ref MotelySingleSearchContext singleCtx) =>
                {
                    int needed = clause.Min;
                    Debug.Assert(needed > 0, "StandardCardClause.Min must be > 0 — loader bug.");

                    int count = 0;
                    var shopItems = clause.Sources.ShopItems;
                    var boosterPacks = clause.Sources.BoosterPacks;

                    foreach (var ante in clause.Antes)
                    {
                        // ── Shop items ──
                        if (shopItems.Length > 0)
                        {
                            var shopStream = singleCtx.CreateShopItemStream(ante);

                            for (int slot = 0; slot <= maxShopItem; slot++)
                            {
                                var item = singleCtx.GetNextShopItem(ref shopStream);
                                bool isTarget = false;
                                for (int i = 0; i < shopItems.Length; i++)
                                {
                                    if (shopItems[i] == slot)
                                    {
                                        isTarget = true;
                                        break;
                                    }
                                }

                                if (
                                    isTarget
                                    && item.TypeCategory == MotelyItemTypeCategory.PlayingCard
                                    && MatchesStandardCard(item, clause)
                                )
                                {
                                    count++;
                                }
                            }
                        }

                        // ── Standard packs ──
                        if (boosterPacks.Length > 0)
                        {
                            var packStream = singleCtx.CreateBoosterPackStream(ante);
                            var cardStream = singleCtx.CreateStandardPackCardStream(ante);

                            for (int p = 0; p <= maxBoosterPack; p++)
                            {
                                var pack = singleCtx.GetNextBoosterPack(ref packStream);
                                bool isTarget = false;
                                for (int i = 0; i < boosterPacks.Length; i++)
                                {
                                    if (boosterPacks[i] == p)
                                    {
                                        isTarget = true;
                                        break;
                                    }
                                }

                                if (
                                    isTarget
                                    && pack.GetPackType() == MotelyBoosterPackType.Standard
                                )
                                {
                                    var contents = singleCtx.GetNextStandardPackContents(
                                        ref cardStream,
                                        pack.GetPackSize()
                                    );
                                    for (int i = 0; i < contents.Length; i++)
                                    {
                                        if (MatchesStandardCard(contents[i], clause))
                                            count++;
                                    }
                                }
                                else if (pack.GetPackType() == MotelyBoosterPackType.Standard)
                                {
                                    singleCtx.GetNextStandardPackContents(
                                        ref cardStream,
                                        pack.GetPackSize()
                                    );
                                }
                            }
                        }

                        if (count >= needed)
                            break;
                    }

                    return count >= needed;
                }
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool MatchesStandardCard(MotelyItem item, StandardCardClause clause)
        {
            if (clause.Rank.HasValue && item.PlayingCardRank != clause.Rank.Value)
                return false;
            if (clause.Suit.HasValue && item.PlayingCardSuit != clause.Suit.Value)
                return false;
            if (clause.Enhancement.HasValue && item.Enhancement != clause.Enhancement.Value)
                return false;
            if (clause.Seal.HasValue && item.Seal != clause.Seal.Value)
                return false;
            if (clause.Edition.HasValue && item.Edition != clause.Edition.Value)
                return false;
            return true;
        }
    }
}
