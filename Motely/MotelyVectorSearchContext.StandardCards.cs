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

    public MotelyItemVector GetNextStandardCard(ref MotelyVectorStandardCardStream stream)
    {
        // Generate random playing card indices
        Vector256<int> cardIndices = GetNextRandomInt(ref stream.CardPrngStream, 0, MotelyEnum<MotelyPlayingCard>.ValueCount);
        
        // Convert indices to playing card enum values
        Vector256<int> playingCards = Vector256.Create(
            (int)MotelyEnum<MotelyPlayingCard>.Values[cardIndices[0]],
            (int)MotelyEnum<MotelyPlayingCard>.Values[cardIndices[1]],
            (int)MotelyEnum<MotelyPlayingCard>.Values[cardIndices[2]],
            (int)MotelyEnum<MotelyPlayingCard>.Values[cardIndices[3]],
            (int)MotelyEnum<MotelyPlayingCard>.Values[cardIndices[4]],
            (int)MotelyEnum<MotelyPlayingCard>.Values[cardIndices[5]],
            (int)MotelyEnum<MotelyPlayingCard>.Values[cardIndices[6]],
            (int)MotelyEnum<MotelyPlayingCard>.Values[cardIndices[7]]
        );
        
        // Add playing card category bits
        playingCards = Vector256.BitwiseOr(playingCards, Vector256.Create((int)MotelyItemTypeCategory.PlayingCard));
        
        MotelyItemVector item = new(playingCards);
        
        // Apply enhancements if enabled
        if (!stream.HasEnhancementPrngStream.IsInvalid)
        {
            Vector512<double> hasEnhancementMask = Vector512.GreaterThan(GetNextRandom(ref stream.HasEnhancementPrngStream), Vector512.Create(0.6));
            if (!Vector512.EqualsAll(hasEnhancementMask, Vector512<double>.Zero))
            {
                Vector256<int> enhancementValues = GetNextRandomInt(ref stream.EnhancementPrngStream, 1, MotelyEnum<MotelyItemEnhancement>.ValueCount, hasEnhancementMask);
                Vector256<int> enhancementMask = Vector256.ShiftLeft(enhancementValues, Motely.ItemEnhancementOffset);
                item = item.WithEnhancement(new VectorEnum256<MotelyItemEnhancement>(MotelyVectorUtils.ShrinkDoubleMaskToInt(hasEnhancementMask) & enhancementMask));
            }
        }
        
        // Apply editions if enabled
        if (!stream.EditionPrngStream.IsInvalid)
        {
            Vector512<double> editionPoll = GetNextRandom(ref stream.EditionPrngStream);
            Vector512<double> hasEditionMask = Vector512.GreaterThan(editionPoll, Vector512.Create(0.96));
            if (!Vector512.EqualsAll(hasEditionMask, Vector512<double>.Zero))
            {
                Vector256<int> editionValues = GetNextRandomInt(ref stream.EditionPrngStream, 1, MotelyEnum<MotelyItemEdition>.ValueCount, hasEditionMask);
                Vector256<int> editionMask = Vector256.ShiftLeft(editionValues, Motely.ItemEditionOffset);
                item = item.WithEdition(new VectorEnum256<MotelyItemEdition>(MotelyVectorUtils.ShrinkDoubleMaskToInt(hasEditionMask) & editionMask));
            }
        }
        
        // Apply seals if enabled
        if (!stream.HasSealPrngStream.IsInvalid)
        {
            Vector512<double> hasSealMask = Vector512.GreaterThan(GetNextRandom(ref stream.HasSealPrngStream), Vector512.Create(0.96));
            if (!Vector512.EqualsAll(hasSealMask, Vector512<double>.Zero))
            {
                Vector256<int> sealValues = GetNextRandomInt(ref stream.SealPrngStream, 1, MotelyEnum<MotelyItemSeal>.ValueCount, hasSealMask);
                Vector256<int> sealMask = Vector256.ShiftLeft(sealValues, Motely.ItemSealOffset);
                item = item.WithSeal(new VectorEnum256<MotelyItemSeal>(MotelyVectorUtils.ShrinkDoubleMaskToInt(hasSealMask) & sealMask));
            }
        }
        
        return item;
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
    
    public VectorMask GetNextStandardPackHasThe(ref MotelyVectorStandardCardStream stream, MotelyPlayingCard targetCard, MotelyBoosterPackSize size)
    {
        int cardCount = MotelyBoosterPackType.Standard.GetCardCount(size);
        VectorMask hasTarget = VectorMask.NoBitsSet;
        
        for (int i = 0; i < cardCount; i++)
        {
            var card = GetNextStandardCard(ref stream);
            // Extract playing card type using bit masking
            var cardType = new VectorEnum256<MotelyPlayingCard>(Vector256.BitwiseAnd(card.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
            VectorMask isTarget = VectorEnum256.Equals(cardType, targetCard);
            hasTarget |= isTarget;
            
            // Early exit optimization - if all lanes have found the target, no need to continue
            if (hasTarget.IsAllTrue())
                break;
        }
        
        return hasTarget;
    }
    
    public VectorMask GetNextStandardPackHasThe(ref MotelyVectorStandardCardStream stream, MotelyPlayingCard[] targetCards, MotelyBoosterPackSize size)
    {
        int cardCount = MotelyBoosterPackType.Standard.GetCardCount(size);
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        
        for (int i = 0; i < cardCount; i++)
        {
            var card = GetNextStandardCard(ref stream);
            // Extract playing card type using bit masking
            var cardType = new VectorEnum256<MotelyPlayingCard>(Vector256.BitwiseAnd(card.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
            
            VectorMask isAnyTarget = VectorMask.NoBitsSet;
            foreach (var target in targetCards)
            {
                isAnyTarget |= VectorEnum256.Equals(cardType, target);
            }
            
            hasAnyTarget |= isAnyTarget;
            
            // Early exit optimization - if all lanes have found any target, no need to continue
            if (hasAnyTarget.IsAllTrue())
                break;
        }
        
        return hasAnyTarget;
    }
}