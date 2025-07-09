using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely;

public ref struct MotelySingleSpectralStream(int ante, bool soulable, MotelySingleResampleStream resampleStream, MotelySinglePrngStream soulStream)
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
    public MotelySingleSpectralStream CreateSpectralPackStreamCached(int ante)
    {
        return new(ante, true,
            CreateResampleStreamCached(MotelyPrngKeys.Spectral + MotelyPrngKeys.SpectralPack + ante),
            CreatePrngStreamCached(MotelyPrngKeys.SpectralSoul + MotelyPrngKeys.Spectral + ante)
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleSpectralStream CreateSpectralPackStream(int ante)
    {
        return new(ante, true,
            CreateResampleStream(MotelyPrngKeys.Spectral + MotelyPrngKeys.SpectralPack + ante),
            CreatePrngStream(MotelyPrngKeys.SpectralSoul + MotelyPrngKeys.Spectral + ante)
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleSpectralStream CreateShopSpectralStreamCached(int ante)
    {
        return new(ante, false,
            CreateResampleStreamCached(MotelyPrngKeys.Spectral + MotelyPrngKeys.Shop + ante),
            default // Shop spectrals can't have Soul/Black Hole
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleSpectralStream CreateShopSpectralStream(int ante)
    {
        return new(ante, false,
            CreateResampleStream(MotelyPrngKeys.Spectral + MotelyPrngKeys.Shop + ante),
            default // Shop spectrals can't have Soul/Black Hole
        );
    }

    public MotelySingleItemSet GetSpectralPackContents(ref MotelySingleSpectralStream spectralStream, MotelyBoosterPackSize size)
        => GetSpectralPackContents(ref spectralStream, size switch
        {
            MotelyBoosterPackSize.Normal => 2,
            MotelyBoosterPackSize.Jumbo => 4,
            MotelyBoosterPackSize.Mega => 4,
            _ => throw new InvalidEnumArgumentException()
        });

    public MotelySingleItemSet GetSpectralPackContents(ref MotelySingleSpectralStream spectralStream, int size)
    {
        Debug.Assert(size <= MotelySingleItemSet.MaxLength);

        MotelySingleItemSet pack = new();

        for (int i = 0; i < size; i++)
            pack.Append(GetNextSpectral(ref spectralStream, pack));

        return pack;
    }

    public MotelyItem GetNextSpectral(ref MotelySingleSpectralStream spectralStream, in MotelySingleItemSet itemSet)
    {
        if (spectralStream.Soulable)
        {
            MotelyItemType forcedKey = default;
            
            // Check for The Soul (0.3% chance)
            if (!itemSet.Contains(MotelyItemType.Soul) && GetNextRandom(ref spectralStream.SoulPrngStream) > 0.997)
            {
                forcedKey = MotelyItemType.Soul;
            }
            
            // Check for Black Hole (0.3% chance) - uses separate random call
            if (!itemSet.Contains(MotelyItemType.BlackHole) && GetNextRandom(ref spectralStream.SoulPrngStream) > 0.997)
            {
                forcedKey = MotelyItemType.BlackHole;
            }
            
            if (forcedKey != default)
                return forcedKey;
        }

        MotelyItemType spectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | 
            (MotelyItemType)GetNextRandomInt(ref spectralStream.ResampleStream.InitialPrngStream, 0, MotelyEnum<MotelySpectralCard>.ValueCount);
        
        int resampleCount = 0;

        while (true)
        {
            if (!itemSet.Contains(spectral))
            {
                return spectral;
            }

            spectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | 
                (MotelyItemType)GetNextRandomInt(
                    ref GetResamplePrngStream(ref spectralStream.ResampleStream, 
                        MotelyPrngKeys.Spectral + (spectralStream.Soulable ? MotelyPrngKeys.SpectralPack : MotelyPrngKeys.Shop) + spectralStream.Ante, 
                        resampleCount),
                    0, MotelyEnum<MotelySpectralCard>.ValueCount
                );

            ++resampleCount;
        }
    }

    public MotelyItem GetNextShopSpectral(ref MotelySingleSpectralStream spectralStream)
    {
        // Shop spectrals are simpler - no duplicate checking needed
        MotelyItemType spectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | 
            (MotelyItemType)GetNextRandomInt(ref spectralStream.ResampleStream.InitialPrngStream, 0, MotelyEnum<MotelySpectralCard>.ValueCount);
        
        return spectral;
    }
}
