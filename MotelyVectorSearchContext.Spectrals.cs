using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

ref partial struct MotelyVectorSearchContext
{
#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyVectorPrngStream CreateSpectralStreamCached(int ante, string source)
    {
        return CreatePrngStreamCached(MotelyPrngKeys.Spectral + source + ante);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyVectorPrngStream CreateSpectralStream(int ante, string source)
    {
        return CreatePrngStream(MotelyPrngKeys.Spectral + source + ante);
    }

    // Helper method for filtering spectral cards in vector context
    public VectorMask FilterSpectralCard(int ante, MotelySpectralCard targetSpectral, string source = MotelyPrngKeys.Shop)
    {
        var spectralStream = CreateSpectralStreamCached(ante, source);
        var spectralChoices = MotelyEnum<MotelySpectralCard>.Values;
        var spectrals = GetNextRandomElement(ref spectralStream, spectralChoices);
        return VectorEnum256.Equals(spectrals, targetSpectral);
    }
}
