namespace Motely;

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

/// <summary>
/// SIMD base filter: Negative edition + legendary-rarity joker on the soul stream (antes 1–2).
/// Legendary rarity is detected via a single bitmask check against
/// <see cref="MotelyJokerRarity.Legendary"/> — no per-type comparisons needed.
/// Compose with <see cref="SoulJokerShopSoulFilterDesc"/> via WithAdditionalFilter.
/// </summary>
public readonly struct NegativeSoulJokerSimdFilterDesc(MotelyItemType? targetJoker = null)
    : IMotelySeedFilterDesc<NegativeSoulJokerSimdFilterDesc.FilterStruct>
{
    public const int MinAnte = 1;
    public const int MaxAnte = 2;

    public readonly FilterStruct CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        for (int ante = MinAnte; ante <= MaxAnte; ante++)
            ctx.CacheSoulJokerStream(
                ante,
                MotelyJokerFixedRarityStreamFlags.ExcludeJokerType
                    | MotelyJokerFixedRarityStreamFlags.ExcludeStickers
            );

        return new FilterStruct(targetJoker);
    }

    public readonly struct FilterStruct(MotelyItemType? targetJoker) : IMotelySeedFilter
    {
        private static readonly Vector256<int> LegendaryRarityBits = Vector256.Create(
            (int)MotelyItemTypeCategory.Joker | (int)MotelyJokerRarity.Legendary
        );

        private static readonly Vector256<int> RarityMask = Vector256.Create(
            MotelyGlobals.ItemTypeCategoryMask | MotelyGlobals.JokerRarityMask
        );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorMask seedMask = VectorMask.NoBitsSet;

            for (int ante = MinAnte; ante <= MaxAnte; ante++)
            {
                if (seedMask.IsAllTrue())
                    break;

                var editionStream = searchContext.CreateSoulJokerStream(
                    ante,
                    MotelyJokerFixedRarityStreamFlags.ExcludeJokerType
                        | MotelyJokerFixedRarityStreamFlags.ExcludeStickers,
                    true
                );
                VectorMask negativeMask = VectorEnum256.Equals(
                    searchContext.GetNextJoker(ref editionStream).Edition,
                    MotelyItemEdition.Negative
                );

                if (negativeMask.IsAllFalse())
                    continue;

                var typeStream = searchContext.CreateSoulJokerStream(
                    ante,
                    MotelyJokerFixedRarityStreamFlags.ExcludeEdition
                        | MotelyJokerFixedRarityStreamFlags.ExcludeStickers,
                    true
                );
                var typeVector = searchContext.GetNextJoker(ref typeStream).Type;

                VectorMask legendaryMask;
                if (targetJoker is { } t)
                    legendaryMask = VectorEnum256.Equals(typeVector, t);
                else
                    legendaryMask = Vector256.Equals(
                        Vector256.BitwiseAnd(typeVector.HardwareVector, RarityMask),
                        LegendaryRarityBits
                    );

                seedMask |= negativeMask & legendaryMask;
            }

            return seedMask;
        }
    }
}

/// <summary>
/// Additional filter after <see cref="NegativeSoulJokerSimdFilterDesc"/>:
/// vectorized The Soul check on arcana/spectral shop packs, with scalar fallback
/// when pack sizes diverge across lanes. Respects <see cref="SoulJokerSourceConfig"/>
/// slot targeting and <see cref="LegendarySoulMatcher"/> stream rules.
/// </summary>
public readonly struct SoulJokerShopSoulFilterDesc(
    SoulJokerSourceConfig? boosterSources = null,
    int[]? searchAntes = null
) : IMotelySeedFilterDesc<SoulJokerShopSoulFilterDesc.FilterStruct>
{
    private static readonly int[] DefaultAntes = [NegativeSoulJokerSimdFilterDesc.MinAnte, NegativeSoulJokerSimdFilterDesc.MaxAnte];

    public readonly FilterStruct CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        SoulJokerSourceConfig normalized =
            (boosterSources ?? new SoulJokerSourceConfig()).NormalizeSoulJokerBoostersIfEmpty();

        int maxPack = normalized.MaxReferencedBoosterSlot();
        int[] antes = searchAntes ?? DefaultAntes;

        for (int i = 0; i < antes.Length; i++)
            ctx.CacheBoosterPackStream(antes[i], force: true);

        return new FilterStruct(normalized, maxPack, antes);
    }

    public readonly struct FilterStruct(
        SoulJokerSourceConfig sources,
        int maxBoosterPack,
        int[] antes
    ) : IMotelySeedFilter
    {
        private readonly SoulJokerSourceConfig _sources = sources;
        private readonly int _maxBoosterPack = maxBoosterPack;
        private readonly int[] _antes = antes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext ctx)
        {
            if (_maxBoosterPack < 0)
                return VectorMask.NoBitsSet;

            int maxP = _maxBoosterPack;
            int[] antes = _antes;
            SoulJokerSourceConfig src = _sources;

            if (TryVectorPath(ref ctx, src, maxP, antes, out VectorMask result))
                return result;

            return ctx.SearchIndividualSeeds(
                (ref MotelySingleSearchContext sctx) =>
                {
                    for (int i = 0; i < antes.Length; i++)
                    {
                        if (LegendarySoulMatcher.MatchAnteShopPackHasSoulOnly(ref sctx, antes[i], src, maxP))
                            return true;
                    }
                    return false;
                }
            );
        }

        private static bool TryVectorPath(
            ref MotelyVectorSearchContext ctx,
            SoulJokerSourceConfig src,
            int maxBoosterPack,
            int[] antes,
            out VectorMask hasSoulMask
        )
        {
            hasSoulMask = VectorMask.NoBitsSet;
            bool split = src.ArcanaBoosterPacks.Length > 0 || src.SpectralBoosterPacks.Length > 0;

            for (int a = 0; a < antes.Length; a++)
            {
                if (hasSoulMask.IsAllTrue())
                    return true;

                int ante = antes[a];
                var packStream = ctx.CreateBoosterPackStream(ante, true, false);

                MotelyVectorTarotStream tarotStream = default;
                MotelyVectorSpectralStream spectralStream = default;
                bool tarotInit = false;
                bool spectralInit = false;

                for (int p = 0; p <= maxBoosterPack; p++)
                {
                    if (hasSoulMask.IsAllTrue())
                        return true;

                    var pack = ctx.GetNextBoosterPack(ref packStream);
                    var packType = pack.GetPackType();
                    var packSize = pack.GetPackSize();

                    bool slotTargeted;
                    if (split)
                    {
                        bool arcSlot = ContainsSlot(src.ArcanaBoosterPacks, p);
                        bool specSlot = ContainsSlot(src.SpectralBoosterPacks, p);
                        slotTargeted = arcSlot || specSlot;
                    }
                    else
                        slotTargeted = ContainsSlot(src.BoosterPacks, p);

                    if (!slotTargeted)
                        continue;

                    VectorMask isArcana = VectorEnum256.Equals(packType, MotelyBoosterPackType.Arcana);
                    VectorMask isSpectral = VectorEnum256.Equals(packType, MotelyBoosterPackType.Spectral);

                    if (src.RequireMegaPack)
                    {
                        VectorMask isMega = VectorEnum256.Equals(packSize, MotelyBoosterPackSize.Mega);
                        isArcana &= isMega;
                        isSpectral &= isMega;
                    }

                    if (split)
                    {
                        if (!ContainsSlot(src.ArcanaBoosterPacks, p))
                            isArcana = VectorMask.NoBitsSet;
                        if (!ContainsSlot(src.SpectralBoosterPacks, p))
                            isSpectral = VectorMask.NoBitsSet;
                    }

                    if (isArcana.IsPartiallyTrue())
                    {
                        if (!TryUniformSize(packSize, isArcana, out var size))
                            return false;

                        if (!tarotInit)
                        {
                            tarotInit = true;
                            tarotStream = ctx.CreateArcanaPackTarotStream(ante, true);
                        }

                        hasSoulMask |= ctx.GetNextArcanaPackHasTheSoul(ref tarotStream, size) & isArcana;
                    }

                    if (isSpectral.IsPartiallyTrue())
                    {
                        if (!TryUniformSize(packSize, isSpectral, out var size))
                            return false;

                        if (!spectralInit)
                        {
                            spectralInit = true;
                            spectralStream = ctx.CreateSpectralPackSpectralStream(ante, soulOnly: ante != 1);
                        }

                        hasSoulMask |= ctx.GetNextSpectralPackHasTheSoul(ref spectralStream, size) & isSpectral;
                    }
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsSlot(int[] slots, int slot)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == slot)
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryUniformSize(
            VectorEnum256<MotelyBoosterPackSize> sizeVector,
            VectorMask laneMask,
            out MotelyBoosterPackSize size
        )
        {
            size = default;
            bool hasAny = false;
            for (int lane = 0; lane < MotelyGlobals.MaxVectorWidth; lane++)
            {
                if (!laneMask[lane])
                    continue;
                var s = sizeVector[lane];
                if (!hasAny)
                {
                    hasAny = true;
                    size = s;
                }
                else if (s != size)
                    return false;
            }
            return hasAny;
        }
    }
}
