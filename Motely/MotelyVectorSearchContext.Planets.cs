using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public ref struct MotelyVectorPlanetStream(string resampleKey, MotelyVectorResampleStream resampleStream, MotelyVectorPrngStream blackHolePrngStream)
{
    public readonly bool IsNull => ResampleStream.IsInvalid;
    public readonly string ResampleKey = resampleKey;
    public MotelyVectorResampleStream ResampleStream = resampleStream;
    public MotelyVectorPrngStream BlackHolePrngStream = blackHolePrngStream;
    public readonly bool IsBlackHoleable => !BlackHolePrngStream.IsInvalid;
}

ref partial struct MotelyVectorSearchContext
{
#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelyVectorPlanetStream CreatePlanetStream(string source, int ante, bool blackHoleable, bool isCached)
    {
        string resampleKey = MotelyPrngKeys.Planet + source + ante;
        return new(
            resampleKey,
            CreateResampleStream(resampleKey, isCached),
            blackHoleable ?
                CreatePrngStream(MotelyPrngKeys.PlanetBlackHole + MotelyPrngKeys.Planet + ante, isCached) :
                MotelyVectorPrngStream.Invalid
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyVectorPlanetStream CreateCelestialPackPlanetStream(int ante, bool isCached = false) =>
        CreatePlanetStream(MotelyPrngKeys.CelestialPackItemSource, ante, true, isCached);

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyVectorPlanetStream CreateShopPlanetStream(int ante, bool isCached = false) =>
        CreatePlanetStream(MotelyPrngKeys.ShopItemSource, ante, false, isCached);

    public MotelyVectorItemSet GetNextCelestialPackContents(ref MotelyVectorPlanetStream planetStream, MotelyBoosterPackSize size)
    {
        int cardCount = MotelyBoosterPackType.Celestial.GetCardCount(size);
        MotelyVectorItemSet pack = new();
        for (int i = 0; i < cardCount; i++)
            pack.Append(GetNextPlanet(ref planetStream, pack));
        return pack;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector GetNextPlanet(ref MotelyVectorPlanetStream planetStream)
    {
        return GetNextPlanet(ref planetStream, Vector512<double>.AllBitsSet);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector GetNextShopPlanetOrNull(ref MotelyVectorPlanetStream planetStream, ref MotelyVectorPrngStream itemTypeStream,
        Vector512<double> totalRate, Vector512<double> tarotRate, Vector512<double> planetRate)
    {
        // Check what type this slot is
        var itemTypePoll = GetNextRandom(ref itemTypeStream) * totalRate;
        itemTypePoll -= Vector512.Create(20.0); // Skip joker range
        itemTypePoll -= tarotRate; // Skip tarot range
        var isPlanetSlot = Vector512.LessThan(itemTypePoll, planetRate);
        
        // Only advance planet stream for planet slots
        var planet = GetNextPlanet(ref planetStream, isPlanetSlot);
        
        // Return planet or None for non-planet slots
        var planetIntMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(isPlanetSlot);
        var noneItem = Vector256<int>.Zero;
        
        return new MotelyItemVector(
            Vector256.ConditionalSelect(planetIntMask, planet.Value, noneItem)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector GetNextPlanet(ref MotelyVectorPlanetStream planetStream, in Vector512<double> mask)
    {
        Vector512<double> blackHoleMask;
        if (planetStream.IsBlackHoleable)
        {
            blackHoleMask = mask & Vector512.GreaterThan(GetNextRandom(ref planetStream.BlackHolePrngStream, mask), Vector512.Create(0.997));
        }
        else
        {
            blackHoleMask = Vector512<double>.Zero;
        }

        Vector256<int> planets;
        if (planetStream.ResampleStream.IsInvalid)
        {
            planets = Vector256.Create(new MotelyItem(MotelyItemType.PlanetExcludedByStream).Value);
        }
        else
        {
            // Only advance PRNG for lanes that need it (using mask and not black hole mask)
            var planetMask = mask & ~blackHoleMask;
            planets = GetNextRandomInt(
                ref planetStream.ResampleStream.InitialPrngStream,
                0, MotelyEnum<MotelyPlanetCard>.ValueCount, planetMask
            );
            planets = Vector256.Create((int)MotelyItemTypeCategory.PlanetCard) | planets;
        }

        if (!planetStream.IsBlackHoleable)
        {
            return new(planets);
        }

        return new(Vector256.ConditionalSelect(
            MotelyVectorUtils.ShrinkDoubleMaskToInt(blackHoleMask),
            Vector256.Create(new MotelyItem(MotelyItemType.BlackHole).Value),
            planets
        ));
    }

    public MotelyItemVector GetNextPlanet(ref MotelyVectorPlanetStream planetStream, in MotelyVectorItemSet itemSet)
    {
        Vector512<double> blackHoleMask;
        Vector256<int> blackHoleMaskInt;
        if (planetStream.IsBlackHoleable)
        {
            Vector512<double> validMask = MotelyVectorUtils.ExtendIntMaskToDouble(~itemSet.Contains(MotelyItemType.BlackHole));
            blackHoleMask = validMask & Vector512.GreaterThan(GetNextRandom(ref planetStream.BlackHolePrngStream, validMask), Vector512.Create(0.997));
            blackHoleMaskInt = MotelyVectorUtils.ShrinkDoubleMaskToInt(blackHoleMask);
        }
        else
        {
            blackHoleMask = Vector512<double>.Zero;
            blackHoleMaskInt = Vector256<int>.Zero;
        }

        Vector256<int> planets;
        if (planetStream.ResampleStream.IsInvalid)
        {
            planets = Vector256.Create(new MotelyItem(MotelyItemType.PlanetExcludedByStream).Value);
        }
        else
        {
            planets = GetNextRandomInt(
                ref planetStream.ResampleStream.InitialPrngStream,
                0, MotelyEnum<MotelyPlanetCard>.ValueCount, ~blackHoleMask
            );
            planets = Vector256.Create((int)MotelyItemTypeCategory.PlanetCard) | planets;

            int resampleCount = 0;
            while (true)
            {
                Vector256<int> resampleMaskInt = itemSet.Contains(new MotelyItemVector(planets));
                resampleMaskInt &= ~blackHoleMaskInt;
                if (Vector256.EqualsAll(resampleMaskInt, Vector256<int>.Zero))
                    break;
                Vector256<int> nextPlanets = GetNextRandomInt(
                    ref GetResamplePrngStream(ref planetStream.ResampleStream, planetStream.ResampleKey, resampleCount),
                    0, MotelyEnum<MotelyPlanetCard>.ValueCount, MotelyVectorUtils.ExtendIntMaskToDouble(resampleMaskInt)
                );
                nextPlanets = Vector256.Create((int)MotelyItemTypeCategory.PlanetCard) | nextPlanets;
                planets = Vector256.ConditionalSelect(
                    resampleMaskInt,
                    nextPlanets, planets
                );
                ++resampleCount;
            }
        }

        return new(Vector256.ConditionalSelect(
            blackHoleMaskInt,
            Vector256.Create(new MotelyItem(MotelyItemType.BlackHole).Value),
            planets
        ));
    }

    public VectorMask GetNextCelestialPackHasThe(ref MotelyVectorPlanetStream planetStream, MotelyPlanetCard targetPlanet, MotelyBoosterPackSize size)
    {
        int cardCount = MotelyBoosterPackType.Celestial.GetCardCount(size);
        VectorMask hasTarget = VectorMask.NoBitsSet;
        
        for (int i = 0; i < cardCount; i++)
        {
            var planet = GetNextPlanet(ref planetStream);
            // Extract planet card type using bit masking (similar to PlayingCardSuit pattern)
            var planetType = new VectorEnum256<MotelyPlanetCard>(Vector256.BitwiseAnd(planet.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
            VectorMask isTarget = VectorEnum256.Equals(planetType, targetPlanet);
            hasTarget |= isTarget;
            
            // Early exit optimization - if all lanes have found the target, no need to continue
            if (hasTarget.IsAllTrue())
                break;
        }
        
        return hasTarget;
    }

    public VectorMask GetNextCelestialPackHasThe(ref MotelyVectorPlanetStream planetStream, MotelyPlanetCard[] targetPlanets, MotelyBoosterPackSize size)
    {
        int cardCount = MotelyBoosterPackType.Celestial.GetCardCount(size);
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        
        for (int i = 0; i < cardCount; i++)
        {
            var planet = GetNextPlanet(ref planetStream);
            // Extract planet card type using bit masking (similar to PlayingCardSuit pattern)
            var planetType = new VectorEnum256<MotelyPlanetCard>(Vector256.BitwiseAnd(planet.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
            
            VectorMask isAnyTarget = VectorMask.NoBitsSet;
            foreach (var target in targetPlanets)
            {
                isAnyTarget |= VectorEnum256.Equals(planetType, target);
            }
            
            hasAnyTarget |= isAnyTarget;
            
            // Early exit optimization - if all lanes have found any target, no need to continue
            if (hasAnyTarget.IsAllTrue())
                break;
        }
        
        return hasAnyTarget;
    }
}
