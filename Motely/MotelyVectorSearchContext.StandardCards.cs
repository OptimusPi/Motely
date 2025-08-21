using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public ref struct MotelyVectorStandardCardStream
{
    public MotelyVectorPrngStream CardPrngStream;
    public MotelyVectorPrngStream HasEnhancementPrngStream;
    public MotelyVectorPrngStream EnhancementPrngStream;
    public MotelyVectorPrngStream EditionPrngStream;
    public MotelyVectorPrngStream HasSealPrngStream;
    public MotelyVectorPrngStream SealPrngStream;

    public readonly bool IsInvalid => CardPrngStream.IsInvalid;
}



unsafe partial struct MotelyVectorSearchContext
{
    public MotelyVectorStandardCardStream CreateStandardPackCardStream(int ante, MotelyStandardCardStreamFlags flags = MotelyStandardCardStreamFlags.Default, bool isCached = false)
    {
        return new MotelyVectorStandardCardStream
        {
            CardPrngStream = CreatePrngStream(MotelyPrngKeys.StandardCardBase + MotelyPrngKeys.StandardPackItemSource + ante, isCached),

            HasEnhancementPrngStream = flags.HasFlag(MotelyStandardCardStreamFlags.ExcludeEnhancement) ?
                MotelyVectorPrngStream.Invalid :
                CreatePrngStream(MotelyPrngKeys.StandardCardHasEnhancement + ante, isCached),
            EnhancementPrngStream = flags.HasFlag(MotelyStandardCardStreamFlags.ExcludeEnhancement) ?
                MotelyVectorPrngStream.Invalid :
                CreatePrngStream(MotelyPrngKeys.StandardCardEnhancement + MotelyPrngKeys.StandardPackItemSource + ante, isCached),

            EditionPrngStream = flags.HasFlag(MotelyStandardCardStreamFlags.ExcludeEdition) ?
                MotelyVectorPrngStream.Invalid :
                CreatePrngStream(MotelyPrngKeys.StandardCardEdition + ante, isCached),

            HasSealPrngStream = flags.HasFlag(MotelyStandardCardStreamFlags.ExcludeSeal) ?
                MotelyVectorPrngStream.Invalid :
                CreatePrngStream(MotelyPrngKeys.StandardCardHasSeal + ante, isCached),
            SealPrngStream = flags.HasFlag(MotelyStandardCardStreamFlags.ExcludeSeal) ?
                MotelyVectorPrngStream.Invalid :
                CreatePrngStream(MotelyPrngKeys.StandardCardSeal + ante, isCached),
        };
    }

    // Note: For vectorized, this might need to return an array or vector of sets, but to match single, perhaps adapt accordingly
    public MotelyItemVector GetNextStandardCard(ref MotelyVectorStandardCardStream stream)
    {
        // For now, return a basic playing card
        // This needs proper implementation based on the PRNG stream
        return new MotelyItemVector(Vector256.Create((int)MotelyItemType.C2));
    }
    
    public MotelyVectorItemSet GetNextStandardPackContents(ref MotelyVectorStandardCardStream stream, MotelyBoosterPackSize size)
    {
        MotelyVectorItemSet pack = new();
        int cardCount = MotelyBoosterPackType.Standard.GetCardCount(size);
        
        for (int i = 0; i < cardCount; i++)
        {
            pack.Append(GetNextStandardCard(ref stream));
        }
        
        return pack;
    }
}