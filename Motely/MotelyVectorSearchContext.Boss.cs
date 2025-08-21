using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public struct MotelyVectorBossStream
{
    public MotelyVectorPrngStream PrngStream;
    public int CurrentAnte;
    
    public MotelyVectorBossStream(MotelyVectorPrngStream prngStream)
    {
        PrngStream = prngStream;
        CurrentAnte = 1;
    }
}

ref partial struct MotelyVectorSearchContext
{
    public MotelyVectorBossStream CreateBossStream(int ante, bool isCached = false)
    {
        // Boss RNG uses "boss" as the key in Balatro (lowercase, no ante appended)
        return new MotelyVectorBossStream(CreatePrngStream("boss", isCached));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public VectorEnum256<MotelyBossBlind> GetNextBoss(ref MotelyVectorBossStream stream)
    {
        // For now, return a simple implementation
        // TODO: Match the actual boss selection algorithm from the game
        var random = GetNextRandom(ref stream.PrngStream);
        var bossIndex = GetNextRandomInt(ref stream.PrngStream, 0, MotelyEnum<MotelyBossBlind>.ValueCount);
        return new VectorEnum256<MotelyBossBlind>(bossIndex);
    }
}