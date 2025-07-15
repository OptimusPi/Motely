

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

    public MotelySingleItemSet GetSpectralPackContents(ref MotelySingleSpectralStream SpectralStream, MotelyBoosterPackSize size)
        => GetSpectralPackContents(ref SpectralStream, size switch
        {
            MotelyBoosterPackSize.Normal => 3,
            MotelyBoosterPackSize.Jumbo => 5,
            MotelyBoosterPackSize.Mega => 5,
            _ => throw new InvalidEnumArgumentException()
        });

    public MotelySingleItemSet GetSpectralPackContents(ref MotelySingleSpectralStream SpectralStream, int size)
    {
        Debug.Assert(size <= MotelySingleItemSet.MaxLength);

        MotelySingleItemSet pack = new();

        for (int i = 0; i < size; i++)
            pack.Append(GetNextSpectral(ref SpectralStream, pack));

        return pack;
    }

    public MotelyItem GetNextSpectral(ref MotelySingleSpectralStream SpectralStream, in MotelySingleItemSet itemSet)
    {
        if (SpectralStream.Soulable && !itemSet.Contains(MotelyItemType.Soul))
        {
            if (GetNextRandom(ref SpectralStream.SoulPrngStream) > 0.997)
            {
                return MotelyItemType.Soul;
            }
        }

        MotelyItemType Spectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)GetNextRandomInt(ref SpectralStream.ResampleStream.InitialPrngStream, 0, MotelyEnum<MotelySpectralCard>.ValueCount);
        int resampleCount = 0;

        while (true)
        {
            if (!itemSet.Contains(Spectral))
            {
                return Spectral;
            }

            Spectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)GetNextRandomInt(
                ref GetResamplePrngStream(ref SpectralStream.ResampleStream, MotelyPrngKeys.Spectral + MotelyPrngKeys.SpectralPack + SpectralStream.Ante, resampleCount),
                0, MotelyEnum<MotelySpectralCard>.ValueCount
            );

            ++resampleCount;
        }
    }
}