namespace Motely;

using System.Runtime.CompilerServices;

internal static class NegativePerkeoSoulShopPaths
{
    internal const int MinAnte = 1;
    internal const int MaxAnte = 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchShopSoulsForNegativePerkeoAntes(
        ref MotelySingleSearchContext searchContext
    )
    {
        for (int ante = MinAnte; ante <= MaxAnte; ante++)
        {
            MotelySingleTarotStream tarotStream = default;
            MotelySingleSpectralStream spectralStream = default;
            bool tarotStreamInit = false,
                spectralStreamInit = false;

            MotelySingleBoosterPackStream boosterPackStream =
                searchContext.CreateBoosterPackStream(ante);

            for (int i = 0; i < 5; i++)
            {
                MotelyBoosterPack pack = searchContext.GetNextBoosterPack(ref boosterPackStream);

                if (pack.GetPackType() == MotelyBoosterPackType.Arcana)
                {
                    if (!tarotStreamInit)
                    {
                        tarotStreamInit = true;
                        tarotStream = searchContext.CreateArcanaPackTarotStream(ante, true);
                    }

                    if (
                        searchContext.GetNextArcanaPackHasTheSoul(
                            ref tarotStream,
                            pack.GetPackSize()
                        )
                    )
                        return true;
                }

                if (pack.GetPackType() == MotelyBoosterPackType.Spectral)
                {
                    if (!spectralStreamInit)
                    {
                        spectralStreamInit = true;
                        spectralStream = searchContext.CreateSpectralPackSpectralStream(ante, true);
                    }

                    if (
                        searchContext.GetNextSpectralPackHasTheSoul(
                            ref spectralStream,
                            pack.GetPackSize()
                        )
                    )
                        return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// SIMD gate: Negative edition + Perkeo on the soul joker stream (antes 1–2).
/// Pair with <see cref="NegativePerkeoShopSoulFilterDesc"/> via
/// <see cref="MotelySearchSettings{T}.WithAdditionalFilter"/> so rare survivors batch before shop/soul scalar work.
/// </summary>
public struct NegativePerkeoSimdFilterDesc()
    : IMotelySeedFilterDesc<NegativePerkeoSimdFilterDesc.FilterStruct>
{
    public readonly FilterStruct CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        for (int ante = NegativePerkeoSoulShopPaths.MinAnte; ante <= NegativePerkeoSoulShopPaths.MaxAnte; ante++)
            ctx.CacheSoulJokerStream(
                ante,
                MotelyJokerFixedRarityStreamFlags.ExcludeJokerType
                    | MotelyJokerFixedRarityStreamFlags.ExcludeStickers
            );

        return new FilterStruct();
    }

    public struct FilterStruct : IMotelySeedFilter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorMask seedMask = VectorMask.NoBitsSet;

            for (int ante = NegativePerkeoSoulShopPaths.MinAnte; ante <= NegativePerkeoSoulShopPaths.MaxAnte; ante++)
            {
                var editionStream = searchContext.CreateSoulJokerStream(
                    ante,
                    MotelyJokerFixedRarityStreamFlags.ExcludeJokerType
                        | MotelyJokerFixedRarityStreamFlags.ExcludeStickers,
                    true
                );
                var editionVector = searchContext.GetNextJoker(ref editionStream).Edition;
                VectorMask negativeMask = VectorEnum256.Equals(
                    editionVector,
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
                VectorMask perkeoMask = VectorEnum256.Equals(typeVector, MotelyItemType.Perkeo);

                seedMask |= negativeMask & perkeoMask;
            }

            return seedMask;
        }
    }
}

/// <summary>
/// Additional filter: after <see cref="NegativePerkeoSimdFilterDesc"/>, verify arcana/spectral shop packs
/// can roll The Soul (same pack stream as the legacy combined filter).
/// </summary>
public struct NegativePerkeoShopSoulFilterDesc()
    : IMotelySeedFilterDesc<NegativePerkeoShopSoulFilterDesc.FilterStruct>
{
    public readonly FilterStruct CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        for (
            int ante = NegativePerkeoSoulShopPaths.MinAnte;
            ante <= NegativePerkeoSoulShopPaths.MaxAnte;
            ante++
        )
            ctx.CacheBoosterPackStream(ante, force: true);

        return new FilterStruct();
    }

    public struct FilterStruct : IMotelySeedFilter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            return searchContext.SearchIndividualSeeds(
                static (ref MotelySingleSearchContext ctx) =>
                    NegativePerkeoSoulShopPaths.MatchShopSoulsForNegativePerkeoAntes(ref ctx)
            );
        }
    }
}

/// <summary>
/// Single-filter convenience: SIMD gate + shop/soul in one pass (no additional-filter batching).
/// Prefer <see cref="NegativePerkeoSimdFilterDesc"/> + <see cref="NegativePerkeoShopSoulFilterDesc"/>.
/// </summary>
public struct NegativePerkeoFilterDescNew()
    : IMotelySeedFilterDesc<NegativePerkeoFilterDescNew.FilterStruct>
{
    public readonly FilterStruct CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        _ = new NegativePerkeoSimdFilterDesc().CreateFilter(ref ctx);
        _ = new NegativePerkeoShopSoulFilterDesc().CreateFilter(ref ctx);
        return new FilterStruct();
    }

    public struct FilterStruct : IMotelySeedFilter
    {
        private static readonly NegativePerkeoSimdFilterDesc.FilterStruct Simd = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            VectorMask simd = Simd.Filter(ref searchContext);
            if (simd.IsAllFalse())
                return simd;

            return searchContext.SearchIndividualSeeds(
                simd,
                static (ref MotelySingleSearchContext ctx) =>
                    NegativePerkeoSoulShopPaths.MatchShopSoulsForNegativePerkeoAntes(ref ctx)
            );
        }
    }
}
