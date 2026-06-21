using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Filters.Jaml;

public sealed class TarotCardClause : JamlClause
{
    public required MotelyTarotCard[] Tarots { get; set; }
    public TarotCardSourceConfig Sources { get; set; } = new();
}

public struct TarotCardFilterDesc(TarotCardClause clause)
    : IMotelySeedFilterDesc<TarotCardFilterDesc.TarotCardFilter>
{
    private readonly TarotCardClause _clause = clause;

    public TarotCardFilter CreateFilter(ref MotelyFilterCreationContext ctx)
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

        int maxEmperor = 0;
        for (int i = 0; i < _clause.Sources.Emperor.Length; i++)
        {
            if (_clause.Sources.Emperor[i] > maxEmperor)
                maxEmperor = _clause.Sources.Emperor[i];
        }

        int maxPurpleSeal = 0;
        for (int i = 0; i < _clause.Sources.PurpleSealOrEightBall.Length; i++)
        {
            if (_clause.Sources.PurpleSealOrEightBall[i] > maxPurpleSeal)
                maxPurpleSeal = _clause.Sources.PurpleSealOrEightBall[i];
        }

        return new TarotCardFilter(_clause, maxShopItem, maxBoosterPack, maxEmperor, maxPurpleSeal);
    }

    public struct TarotCardFilter(
        TarotCardClause clause,
        int maxShopItem,
        int maxBoosterPack,
        int maxEmperor,
        int maxPurpleSeal
    ) : IMotelySeedFilter
    {
        private readonly TarotCardClause _clause = clause;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;
        private readonly int _maxEmperor = maxEmperor;
        private readonly int _maxPurpleSeal = maxPurpleSeal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.Tarots.Length > 0);
            var clause = _clause;
            int maxShopItem = _maxShopItem;
            int maxBoosterPack = _maxBoosterPack;
            int maxEmperor = _maxEmperor;
            int maxPurpleSeal = _maxPurpleSeal;
            int needed = clause.Min;
            Debug.Assert(needed > 0, "TarotCardClause.Min must be > 0 — loader bug.");

            Vector256<int> matchCounts = Vector256<int>.Zero;
            var shopIndices = clause.Sources.ShopItems;
            var boosterPacks = clause.Sources.BoosterPacks;
            var emperorRolls = clause.Sources.Emperor;
            var sealRolls = clause.Sources.PurpleSealOrEightBall;

            foreach (var ante in clause.Antes)
            {
                // ── Shop items SIMD ──
                if (shopIndices.Length > 0)
                {
                    var shopStream = ctx.CreateShopItemStream(ante);

                    for (int slot = 0; slot <= maxShopItem; slot++)
                    {
                        var item = ctx.GetNextShopItem(ref shopStream);
                        bool isTarget = false;
                        for (int i = 0; i < shopIndices.Length; i++)
                        {
                            if (shopIndices[i] == slot)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (!isTarget)
                            continue;

                        VectorMask isTarot = VectorEnum256.Equals(
                            item.TypeCategory,
                            MotelyItemTypeCategory.TarotCard
                        );
                        VectorMask match = MatchTarots(item, clause) & isTarot;

                        if (match.IsPartiallyTrue())
                        {
                            matchCounts = Vector256.Add(
                                matchCounts,
                                Vector256.ConditionalSelect(
                                    MotelyVectorUtils.VectorMaskToConditionalSelectMask(match),
                                    Vector256.Create(1),
                                    Vector256<int>.Zero
                                )
                            );
                        }
                    }
                }

                // ── Arcana packs SIMD ──
                // Note: GetNextArcanaPackContents takes scalar MotelyBoosterPackSize.
                // Pack size varies per lane, so we process each size variant separately.
                if (boosterPacks.Length > 0)
                {
                    var packStream = ctx.CreateBoosterPackStream(ante);
                    var tarotStream = ctx.CreateArcanaPackTarotStream(ante);

                    // SIMD prefilter is intentionally over-permissive: iterating past ante 1's real
                    // pack count (4) yields phantom matches from the PRNG stream, but those are
                    // rejected in the scoring phase which re-verifies scalar per-ante.
                    for (int p = 0; p <= maxBoosterPack; p++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref packStream);
                        bool isTarget = false;
                        for (int i = 0; i < boosterPacks.Length; i++)
                        {
                            if (boosterPacks[i] == p)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        var packType = pack.GetPackType();
                        VectorMask isArcana = VectorEnum256.Equals(
                            packType,
                            MotelyBoosterPackType.Arcana
                        );
                        if (isArcana.IsPartiallyTrue())
                        {
                            // Use Normal size (3 cards) as the baseline — all Arcana packs
                            // have at least 3 cards. Jumbo/Mega have 5.
                            var contents = ctx.GetNextArcanaPackContents(
                                ref tarotStream,
                                MotelyBoosterPackSize.Normal
                            );

                            if (isTarget)
                            {
                                for (int i = 0; i < contents.Length; i++)
                                {
                                    VectorMask match = MatchTarots(contents[i], clause);
                                    if (match.IsPartiallyTrue())
                                    {
                                        matchCounts = Vector256.Add(
                                            matchCounts,
                                            Vector256.ConditionalSelect(
                                                MotelyVectorUtils.VectorMaskToConditionalSelectMask(
                                                    match
                                                ),
                                                Vector256.Create(1),
                                                Vector256<int>.Zero
                                            )
                                        );
                                    }
                                }
                            }
                        }
                    }
                }

                // ── Emperor SIMD ──
                if (emperorRolls.Length > 0)
                {
                    var emperorStream = ctx.CreateEmperorTarotStream(ante);

                    for (int roll = 0; roll <= maxEmperor; roll++)
                    {
                        var tarots = ctx.GetNextEmperorTarots(ref emperorStream);
                        bool isTarget = false;
                        for (int i = 0; i < emperorRolls.Length; i++)
                        {
                            if (emperorRolls[i] == roll)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (isTarget)
                        {
                            VectorMask match1 = MatchTarots(tarots[0], clause);
                            VectorMask match2 = MatchTarots(tarots[1], clause);

                            if (match1.IsPartiallyTrue())
                                matchCounts = Vector256.Add(
                                    matchCounts,
                                    Vector256.ConditionalSelect(
                                        MotelyVectorUtils.VectorMaskToConditionalSelectMask(match1),
                                        Vector256.Create(1),
                                        Vector256<int>.Zero
                                    )
                                );
                            if (match2.IsPartiallyTrue())
                                matchCounts = Vector256.Add(
                                    matchCounts,
                                    Vector256.ConditionalSelect(
                                        MotelyVectorUtils.VectorMaskToConditionalSelectMask(match2),
                                        Vector256.Create(1),
                                        Vector256<int>.Zero
                                    )
                                );
                        }
                    }
                }

                // ── Purple Seal SIMD ──
                if (sealRolls.Length > 0)
                {
                    var purpleSealStream = ctx.CreatePurpleSealTarotStream(ante);

                    for (int roll = 0; roll <= maxPurpleSeal; roll++)
                    {
                        var item = ctx.GetNextTarot(ref purpleSealStream);
                        bool isTarget = false;
                        for (int i = 0; i < sealRolls.Length; i++)
                        {
                            if (sealRolls[i] == roll)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (isTarget)
                        {
                            VectorMask match = MatchTarots(item, clause);
                            if (match.IsPartiallyTrue())
                            {
                                matchCounts = Vector256.Add(
                                    matchCounts,
                                    Vector256.ConditionalSelect(
                                        MotelyVectorUtils.VectorMaskToConditionalSelectMask(match),
                                        Vector256.Create(1),
                                        Vector256<int>.Zero
                                    )
                                );
                            }
                        }
                    }
                }
            }

            Vector256<int> comparison = Vector256.GreaterThan(
                matchCounts,
                Vector256.Subtract(Vector256.Create(needed), Vector256.Create(1))
            );
            return new VectorMask(MotelyVectorUtils.VectorizedComparisonToMask(comparison));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VectorMask MatchTarots(MotelyItemVector items, TarotCardClause clause)
        {
            VectorMask mask = VectorMask.NoBitsSet;
            var itemTypes = items.Type;

            for (int i = 0; i < clause.Tarots.Length; i++)
            {
                var targetType = (int)MotelyItemTypeCategory.TarotCard | (int)clause.Tarots[i];
                mask |= VectorEnum256.Equals(itemTypes, (MotelyItemType)targetType);
            }

            return mask;
        }
    }
}
