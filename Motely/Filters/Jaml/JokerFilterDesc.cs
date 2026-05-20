using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using static Motely.MotelyVectorUtils;

namespace Motely.Filters.Jaml;

public sealed class JokerClause : JamlClause
{
    public MotelyJoker[] Jokers { get; init; } = [];
    public bool IsWildcard { get; init; }
    public MotelyItemEdition? Edition { get; init; }
    public MotelyJokerSticker[] Stickers { get; init; } = [];
    public JokerSourceConfig Sources { get; init; } = new();
    public LegendaryJokerSourceConfig LegendarySources { get; init; } = new();

    public override int EstimatedCost => 6 + MaxAnte;
    public override string Describe() =>
        IsWildcard ? "joker Any"
                   : $"joker {string.Join(", ", System.Array.ConvertAll(Jokers, static j => j.ToString()))}";
    public override IMotelySeedFilterDesc CreateDesc() => new JokerFilterDesc(this);
}

public struct JokerFilterDesc(JokerClause clause)
    : IMotelySeedFilterDesc<JokerFilterDesc.JokerFilter>
{
    private readonly JokerClause _clause = clause;

    public JokerFilter CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        foreach (var ante in _clause.Antes)
        {
            ctx.CacheShopStream(ante);
            ctx.CacheBoosterPackStream(ante);
        }

        // Pre-calculate target item types to avoid bitwise logic in the hot loop
        var targetTypes = new MotelyItemType[_clause.Jokers.Length];
        for (int i = 0; i < _clause.Jokers.Length; i++)
        {
            if (Enum.TryParse(_clause.Jokers[i].ToString(), out MotelyItemType type))
            {
                targetTypes[i] = type;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Joker {_clause.Jokers[i]} not found in MotelyItemType"
                );
            }
        }

        // Extract source indices from config
        var shopIndices = _clause.Sources.ShopItems;
        var boosterIndices = _clause.Sources.BoosterPacks;

        Debug.Assert(shopIndices.Length > 0 || boosterIndices.Length > 0,
            "Joker clause should have normalized default sources at config load time.");

        int maxShopItem = 0;
        foreach (var idx in shopIndices)
            if (idx > maxShopItem)
                maxShopItem = idx;

        int maxBoosterPack = 0;
        foreach (var idx in boosterIndices)
            if (idx > maxBoosterPack)
                maxBoosterPack = idx;

        return new JokerFilter(
            _clause,
            targetTypes,
            shopIndices.ToArray(),
            boosterIndices.ToArray(),
            maxShopItem,
            maxBoosterPack
        );
    }

    public struct JokerFilter(
        JokerClause clause,
        MotelyItemType[] targetTypes,
        int[] shopIndices,
        int[] boosterIndices,
        int maxShopItem,
        int maxBoosterPack
    ) : IMotelySeedFilter
    {
        private readonly JokerClause _clause = clause;
        private readonly MotelyItemType[] _targetTypes = targetTypes;
        private readonly int[] _shopIndices = shopIndices;
        private readonly int[] _boosterIndices = boosterIndices;
        private readonly int _maxShopItem = maxShopItem;
        private readonly int _maxBoosterPack = maxBoosterPack;

        [MethodImpl(
            MethodImplOptions.AggressiveInlining
        )]
        public VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            Debug.Assert(_clause.IsWildcard || _clause.Jokers.Length > 0);
            int needed = _clause.Min;
            Debug.Assert(needed > 0, "JokerClause.Min must be > 0 — loader bug.");

            if (UsesLegendaryPath(_clause))
            {
                var clause = _clause;
                return ctx.SearchIndividualSeeds(
                    (ref MotelySingleSearchContext singleCtx) =>
                        JamlScoring.CountJokerClauseOccurrencesForFilter(ref singleCtx, clause) >= needed
                );
            }

            Vector256<int> matchCounts = Vector256<int>.Zero;

            var shopIndices = _shopIndices;
            var boosterIndices = _boosterIndices;

            foreach (var ante in _clause.Antes)
            {
                // ── Shop items SIMD ──
                if (shopIndices.Length > 0)
                {
                    var shopStream = ctx.CreateShopItemStream(ante);

                    for (int slot = 0; slot <= _maxShopItem; slot++)
                    {
                        var shopItem = ctx.GetNextShopItem(ref shopStream);
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

                        VectorMask jokerMatch = MatchJokers(shopItem);
                        if (jokerMatch.IsPartiallyTrue())
                        {
                            matchCounts = Vector256.Add(
                                matchCounts,
                                Vector256.ConditionalSelect(
                                    VectorMaskToConditionalSelectMask(jokerMatch),
                                    Vector256.Create(1),
                                    Vector256<int>.Zero
                                )
                            );
                        }
                    }
                }

                // ── Buffoon packs SIMD ──
                if (boosterIndices.Length > 0)
                {
                    var packStream = ctx.CreateBoosterPackStream(ante);
                    var jokerStream = ctx.CreateBuffoonPackJokerStream(ante);

                    for (int p = 0; p <= _maxBoosterPack; p++)
                    {
                        var pack = ctx.GetNextBoosterPack(ref packStream);
                        bool isTarget = false;
                        for (int i = 0; i < boosterIndices.Length; i++)
                        {
                            if (boosterIndices[i] == p)
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        VectorMask isBuffoon = VectorEnum256.Equals(
                            pack.GetPackType(),
                            MotelyBoosterPackType.Buffoon
                        );

                        if (isBuffoon.IsPartiallyTrue())
                        {
                            VectorMask isNormalSize = VectorEnum256.Equals(
                                pack.GetPackSize(),
                                MotelyBoosterPackSize.Normal
                            );
                            VectorMask isJumboSize = VectorEnum256.Equals(
                                pack.GetPackSize(),
                                MotelyBoosterPackSize.Jumbo
                            );
                            VectorMask isMegaSize = VectorEnum256.Equals(
                                pack.GetPackSize(),
                                MotelyBoosterPackSize.Mega
                            );

                            if ((isBuffoon & isNormalSize).IsPartiallyTrue())
                            {
                                var contents = ctx.GetNextBuffoonPackContents(
                                    ref jokerStream,
                                    MotelyBoosterPackSize.Normal
                                );
                                MatchBuffoonContents(contents, isTarget, ref matchCounts);
                            }

                            if ((isBuffoon & isJumboSize).IsPartiallyTrue())
                            {
                                var contents = ctx.GetNextBuffoonPackContents(
                                    ref jokerStream,
                                    MotelyBoosterPackSize.Jumbo
                                );
                                MatchBuffoonContents(contents, isTarget, ref matchCounts);
                            }

                            if ((isBuffoon & isMegaSize).IsPartiallyTrue())
                            {
                                var contents = ctx.GetNextBuffoonPackContents(
                                    ref jokerStream,
                                    MotelyBoosterPackSize.Mega
                                );
                                MatchBuffoonContents(contents, isTarget, ref matchCounts);
                            }
                        }
                    }
                }
            }

            Vector256<int> minVec = Vector256.Create(_clause.Min);
            Vector256<int> comparison = Vector256.GreaterThan(
                matchCounts,
                Vector256.Subtract(minVec, Vector256.Create(1))
            );
            return new VectorMask(MotelyVectorUtils.VectorizedComparisonToMask(comparison));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly VectorMask MatchJokers(in MotelyItemVector item)
        {
            VectorMask jokerMatch;
            if (_clause.IsWildcard)
            {
                jokerMatch = VectorEnum256.Equals(item.TypeCategory, MotelyItemTypeCategory.Joker);
            }
            else
            {
                jokerMatch = VectorMask.NoBitsSet;
                for (int t = 0; t < _targetTypes.Length; t++)
                    jokerMatch |= VectorEnum256.Equals(item.Type, _targetTypes[t]);
            }

            if (_clause.Edition.HasValue)
                jokerMatch &= VectorEnum256.Equals(item.Edition, _clause.Edition.Value);

            if (_clause.Stickers.Length > 0)
            {
                VectorMask stickerMatch = VectorMask.NoBitsSet;
                for (int s = 0; s < _clause.Stickers.Length; s++)
                {
                    switch (_clause.Stickers[s])
                    {
                        case MotelyJokerSticker.Eternal:
                            stickerMatch |= item.IsEternal;
                            break;
                        case MotelyJokerSticker.Perishable:
                            stickerMatch |= item.IsPerishable;
                            break;
                        case MotelyJokerSticker.Rental:
                            stickerMatch |= item.IsRental;
                            break;
                    }
                }
                jokerMatch &= stickerMatch;
            }

            return jokerMatch;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void MatchBuffoonContents(
            in MotelyVectorItemSet contents,
            bool isTarget,
            ref Vector256<int> matchCounts
        )
        {
            if (!isTarget)
                return;

            for (int i = 0; i < contents.Length; i++)
            {
                VectorMask match = MatchJokers(contents[i]);
                matchCounts = Vector256.Add(
                    matchCounts,
                    Vector256.ConditionalSelect(
                        VectorMaskToConditionalSelectMask(match),
                        Vector256.Create(1),
                        Vector256<int>.Zero
                    )
                );
            }
        }

        private static bool UsesLegendaryPath(JokerClause clause)
        {
            if (clause.IsWildcard)
                return true;

            for (int i = 0; i < clause.Jokers.Length; i++)
            {
                if (((MotelyJokerRarity)((int)clause.Jokers[i] & MotelyGlobals.JokerRarityMask)) == MotelyJokerRarity.Legendary)
                    return true;
            }

            return false;
        }
    }
}

// ── Rarity-specific joker clauses ──

public sealed class CommonJokerClause : JamlClause
{
    public MotelyJokerCommon[] Jokers { get; init; } = [];
    public bool IsWildcard { get; init; }
    public MotelyItemEdition? Edition { get; init; }
    public MotelyJokerSticker[] Stickers { get; init; } = [];
    public JokerSourceConfig Sources { get; init; } = new();

    public override int EstimatedCost => 6 + MaxAnte;
    public override string Describe() =>
        IsWildcard ? "commonJoker Any"
                   : $"commonJoker {string.Join(", ", System.Array.ConvertAll(Jokers, static j => j.ToString()))}";
    public override IMotelySeedFilterDesc CreateDesc() => new CommonJokerFilterDesc(this);
}

public sealed class UncommonJokerClause : JamlClause
{
    public MotelyJokerUncommon[] Jokers { get; init; } = [];
    public bool IsWildcard { get; init; }
    public MotelyItemEdition? Edition { get; init; }
    public MotelyJokerSticker[] Stickers { get; init; } = [];
    public JokerSourceConfig Sources { get; init; } = new();

    public override int EstimatedCost => 6 + MaxAnte;
    public override string Describe() =>
        IsWildcard ? "uncommonJoker Any"
                   : $"uncommonJoker {string.Join(", ", System.Array.ConvertAll(Jokers, static j => j.ToString()))}";
    public override IMotelySeedFilterDesc CreateDesc() => new UncommonJokerFilterDesc(this);
}

public sealed class RareJokerClause : JamlClause
{
    public MotelyJokerRare[] Jokers { get; init; } = [];
    public bool IsWildcard { get; init; }
    public MotelyItemEdition? Edition { get; init; }
    public MotelyJokerSticker[] Stickers { get; init; } = [];
    public JokerSourceConfig Sources { get; init; } = new();

    public override int EstimatedCost => 5 + MaxAnte;
    public override string Describe() =>
        IsWildcard ? "rareJoker Any"
                   : $"rareJoker {string.Join(", ", System.Array.ConvertAll(Jokers, static j => j.ToString()))}";
    public override IMotelySeedFilterDesc CreateDesc() => new RareJokerFilterDesc(this);
}

public sealed class MixedJokerClause : JamlClause
{
    public MotelyJoker[] Jokers { get; init; } = [];
    public bool IsWildcard { get; init; }
    public MotelyItemEdition? Edition { get; init; }
    public MotelyJokerSticker[] Stickers { get; init; } = [];
    public JokerSourceConfig Sources { get; init; } = new();

    public override int EstimatedCost => 6 + MaxAnte;
    public override string Describe() =>
        IsWildcard ? "jokers Any"
                   : $"jokers {string.Join(", ", System.Array.ConvertAll(Jokers, static j => j.ToString()))}";
    public override IMotelySeedFilterDesc CreateDesc() => new MixedJokerFilterDesc(this);
}
