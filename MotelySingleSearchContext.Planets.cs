using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely;

public ref struct MotelySinglePlanetStream(int ante, bool soulable, MotelySingleResampleStream resampleStream, MotelySinglePrngStream soulStream)
{
    public readonly bool Soulable = soulable;
    public readonly int Ante = ante;
    public MotelySingleResampleStream ResampleStream = resampleStream;
    public MotelySinglePrngStream SoulPrngStream = soulStream;
}

ref partial struct MotelySingleSearchContext
{
#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySinglePlanetStream CreateCelestialPackPlanetStreamCached(int ante)
    {
        return new(ante, true,
            CreateResampleStreamCached(MotelyPrngKeys.Planet + MotelyPrngKeys.CelestialPack + ante),
            CreatePrngStreamCached(MotelyPrngKeys.Planet + MotelyPrngKeys.CelestialPack + ante)
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySinglePlanetStream CreateCelestialPackPlanetStream(int ante)
    {
        return new(ante, true,
            CreateResampleStream(MotelyPrngKeys.Planet + MotelyPrngKeys.CelestialPack + ante),
            CreatePrngStream(MotelyPrngKeys.Planet + MotelyPrngKeys.CelestialPack + ante)
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySinglePlanetStream CreateShopPlanetStreamCached(int ante)
    {
        return new(ante, false,
            CreateResampleStreamCached(MotelyPrngKeys.Planet + MotelyPrngKeys.Shop + ante),
            default // Shop planets can't have Black Hole
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySinglePlanetStream CreateShopPlanetStream(int ante)
    {
        return new(ante, false,
            CreateResampleStream(MotelyPrngKeys.Planet + MotelyPrngKeys.Shop + ante),
            default // Shop planets can't have Black Hole
        );
    }

    public MotelySingleItemSet GetCelestialPackContents(ref MotelySinglePlanetStream planetStream, MotelyBoosterPackSize size)
        => GetCelestialPackContents(ref planetStream, size switch
        {
            MotelyBoosterPackSize.Normal => 3,
            MotelyBoosterPackSize.Jumbo => 5,
            MotelyBoosterPackSize.Mega => 5,
            _ => throw new InvalidEnumArgumentException()
        });

    public MotelySingleItemSet GetCelestialPackContents(ref MotelySinglePlanetStream planetStream, int size)
    {
        Debug.Assert(size <= MotelySingleItemSet.MaxLength);

        MotelySingleItemSet pack = new();

        for (int i = 0; i < size; i++)
            pack.Append(GetNextPlanet(ref planetStream, pack));

        return pack;
    }

    public MotelyItem GetNextPlanet(ref MotelySinglePlanetStream planetStream, in MotelySingleItemSet itemSet)
    {
        // Check for Black Hole (special planet card)
        if (planetStream.Soulable && !itemSet.Contains(MotelyItemType.BlackHole))
        {
            if (GetNextRandom(ref planetStream.SoulPrngStream) > 0.997) // 0.3% chance
            {
                return MotelyItemType.BlackHole;
            }
        }

        MotelyItemType planet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | 
            (MotelyItemType)GetNextRandomInt(ref planetStream.ResampleStream.InitialPrngStream, 0, MotelyEnum<MotelyPlanetCard>.ValueCount);
        
        int resampleCount = 0;

        while (true)
        {
            if (!itemSet.Contains(planet))
            {
                return planet;
            }

            planet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | 
                (MotelyItemType)GetNextRandomInt(
                    ref GetResamplePrngStream(ref planetStream.ResampleStream, 
                        MotelyPrngKeys.Planet + (planetStream.Soulable ? MotelyPrngKeys.CelestialPack : MotelyPrngKeys.Shop) + planetStream.Ante, 
                        resampleCount),
                    0, MotelyEnum<MotelyPlanetCard>.ValueCount
                );

            ++resampleCount;
        }
    }

    public MotelyItem GetNextShopPlanet(ref MotelySinglePlanetStream planetStream)
    {
        // Shop planets are simpler - no duplicate checking needed
        MotelyItemType planet = (MotelyItemType)MotelyItemTypeCategory.PlanetCard | 
            (MotelyItemType)GetNextRandomInt(ref planetStream.ResampleStream.InitialPrngStream, 0, MotelyEnum<MotelyPlanetCard>.ValueCount);
        
        return planet;
    }
}
