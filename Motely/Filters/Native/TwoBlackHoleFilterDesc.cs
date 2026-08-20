using System.Runtime.Intrinsics;

namespace Motely.Filters.Native;

/// <summary>
/// Immolate throwback — port of <c>2blackhole.cl</c>.
///
/// At ante 1: the first booster pack (always a Buffoon pack) must contain
/// <see cref="MotelyItemType.Showman"/>, and the second booster pack must be a
/// Celestial pack containing at least <see cref="MinBlackHoles"/> Black Hole cards.
///
/// The original <c>.cl</c> set <c>inst-&gt;params.showman = true</c> so duplicate
/// cards are allowed within a pack — without it you can never see two Black Holes
/// in one Celestial pack. In Motely there is no Showman state flag wired into the
/// single-seed pack readers; instead the duplicate-suppressing behaviour is the
/// <c>in MotelySingleItemSet</c> overloads. To reproduce <c>showman = true</c> we
/// read the Celestial pack with the plain <see cref="MotelySingleSearchContext.GetNextPlanet"/>
/// overload (no item set) so Black Hole is not de-duplicated.
///
/// SIMD base → SIMD narrow → individual seed introspection — the same shape as
/// <see cref="PerkeoObservatoryFilterDesc"/>.
/// </summary>
public struct TwoBlackHoleFilterDesc()
    : IMotelySeedFilterDesc<TwoBlackHoleFilterDesc.TwoBlackHoleFilter>
{
    /// <summary>The <c>.cl</c> returned the count when <c>blackHoles &gt;= 2</c>; this is that cutoff.</summary>
    public const int MinBlackHoles = 2;

    public readonly TwoBlackHoleFilter CreateFilter(ref MotelyFilterCreationContext ctx) => new();

    public struct TwoBlackHoleFilter() : IMotelySeedFilter
    {
        public readonly VectorMask Filter(ref MotelyVectorSearchContext searchContext)
        {
            // --- Vector pre-narrow: ante-1 second booster pack is a Celestial pack. ---
            // The first ante-1 pack is always forced to Buffoon, so read past it,
            // then keep only lanes whose second pack is Celestial.
            MotelyVectorBoosterPackStream packStream = searchContext.CreateBoosterPackStream(1);
            searchContext.GetNextBoosterPack(ref packStream); // pack 1: always Buffoon
            VectorEnum256<MotelyBoosterPack> secondPack = searchContext.GetNextBoosterPack(
                ref packStream
            );

            VectorMask matching = VectorEnum256.Equals(
                secondPack.GetPackType(),
                MotelyBoosterPackType.Celestial
            );

            if (matching.IsAllFalse())
                return Vector512<double>.Zero;

            // --- Individual seed introspection on the survivors. ---
            return searchContext.SearchIndividualSeeds(
                matching,
                (MotelySingleSearchContext ctx) =>
                {
                    MotelySingleBoosterPackStream packs = ctx.CreateBoosterPackStream(1);

                    // Pack 1 — the guaranteed Buffoon pack — must contain Showman.
                    MotelyBoosterPack buffoonPack = ctx.GetNextBoosterPack(ref packs);
                    MotelySingleJokerStream jokerStream = ctx.CreateBuffoonPackJokerStream(1);

                    bool showmanFound = false;
                    int buffoonCards = buffoonPack.GetPackCardCount();
                    for (int i = 0; i < buffoonCards; i++)
                    {
                        if (ctx.GetNextJoker(ref jokerStream).Type == MotelyItemType.Showman)
                            showmanFound = true;
                    }

                    if (!showmanFound)
                        return 0;

                    // Pack 2 — must be a Celestial pack (the vector narrow already
                    // guaranteed this for the lane, re-check on the scalar stream).
                    MotelyBoosterPack celestialPack = ctx.GetNextBoosterPack(ref packs);
                    if (celestialPack.GetPackType() != MotelyBoosterPackType.Celestial)
                        return 0;

                    // Count Black Holes WITHOUT de-duplication (showman = true): use the
                    // no-item-set GetNextPlanet overload so Black Hole can repeat in-pack.
                    MotelySinglePlanetStream planetStream = ctx.CreateCelestialPackPlanetStream(1);
                    int celestialCards = celestialPack.GetPackCardCount();
                    int blackHoles = 0;
                    for (int i = 0; i < celestialCards; i++)
                    {
                        if (ctx.GetNextPlanet(ref planetStream).Type == MotelyItemType.BlackHole)
                            blackHoles++;
                    }

                    return (blackHoles >= MinBlackHoles) ? 1 : 0;
                }
            );
        }
    }
}
