using System.ComponentModel;


namespace Motely;

public ref struct MotelySinglePlayingCardsStream(int ante, bool foilable, MotelySingleResampleStream resampleStream, MotelySinglePrngStream foilStream)
{
    public readonly bool Foilable = foilable;
    public readonly int Ante = ante;
    public MotelySingleResampleStream ResampleStream = resampleStream;
    public MotelySinglePrngStream FoilPrngStream = foilStream;
}

ref partial struct MotelySingleSearchContext
{
#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySinglePlayingCardsStream CreateStandardPackPlayingCardsStream(int ante)
    {
        return new(ante, true,
            CreateResampleStream(MotelyPrngKeys.Card + ante),
            CreatePrngStream(MotelyPrngKeys.StandardEdition + ante)
        );
    }

    
    public IEnumerable<MotelyItem> GetStandardPackContents(MotelyBoosterPackSize packSize)
    {
        return MotelyBoosterPack.Standard.GetStandardPackContents(
            MotelyItemTypeCategory.PlayingCard
        );
    }
}