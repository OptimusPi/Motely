using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Motely;

public ref struct MotelyVectorSpectralStream(string resampleKey, MotelyVectorResampleStream resampleStream, MotelyVectorPrngStream soulBlackHolePrngStream)
{
    public readonly bool IsNull => ResampleStream.IsInvalid;
    public readonly string ResampleKey = resampleKey;
    public MotelyVectorResampleStream ResampleStream = resampleStream;
    public MotelyVectorPrngStream SoulBlackHolePrngStream = soulBlackHolePrngStream;
    public readonly bool IsSoulBlackHoleable => !SoulBlackHolePrngStream.IsInvalid;
}

ref partial struct MotelyVectorSearchContext
{

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelyVectorSpectralStream CreateSpectralStream(string source, int ante, bool searchSpectral, bool soulBlackHoleable, bool isCached)
    {
        return new(
            MotelyPrngKeys.Spectral + source + ante,
            searchSpectral ? CreateResampleStream(MotelyPrngKeys.Spectral + source + ante, isCached) : MotelyVectorResampleStream.Invalid,
            soulBlackHoleable ? CreatePrngStream(MotelyPrngKeys.SpectralSoulBlackHole + MotelyPrngKeys.Spectral + ante, isCached) : MotelyVectorPrngStream.Invalid
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyVectorSpectralStream CreateShopSpectralStream(int ante, bool isCached = false) =>
        CreateSpectralStream(MotelyPrngKeys.ShopItemSource, ante, true, false, isCached);
    
    public MotelyVectorSpectralStream CreateSpectralPackSpectralStream(int ante, bool soulOnly = false, bool isCached = false) =>
        CreateSpectralStream(MotelyPrngKeys.SpectralPackItemSource, ante, !soulOnly, true, isCached);

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItemVector GetNextSpectral(ref MotelyVectorSpectralStream stream)
    {
        if (stream.IsNull) 
        {
            if (DebugLogger.IsEnabled)
                DebugLogger.Log($"[SPECTRAL] Stream is null! ResampleKey={stream.ResampleKey}");
            return new MotelyItemVector(Vector256<int>.Zero);
        }

        Vector256<int> items = Vector256<int>.Zero;
        Vector512<double> activeMask = Vector512<double>.AllBitsSet;
        
        if (DebugLogger.IsEnabled)
            DebugLogger.Log($"[SPECTRAL] Initial activeMask={activeMask}");

        if (stream.IsSoulBlackHoleable)
        {
            Vector512<double> randomSoul = GetNextRandom(ref stream.SoulBlackHolePrngStream, activeMask);
            Vector512<double> maskSoul = Vector512.GreaterThan(randomSoul, Vector512.Create(0.997));
            Vector256<int> maskSoulInt = MotelyVectorUtils.ShrinkDoubleMaskToInt(maskSoul);
            items = Vector256.ConditionalSelect(maskSoulInt, Vector256.Create((int)MotelyItemType.Soul), items);
            activeMask = Vector512.AndNot(activeMask, maskSoul);

            if (!Vector512.EqualsAll(activeMask, Vector512<double>.Zero))
            {
                Vector512<double> randomBH = GetNextRandom(ref stream.SoulBlackHolePrngStream, activeMask);
                Vector512<double> maskBH = Vector512.GreaterThan(randomBH, Vector512.Create(0.997));
                Vector256<int> maskBHInt = MotelyVectorUtils.ShrinkDoubleMaskToInt(maskBH);
                items = Vector256.ConditionalSelect(maskBHInt, Vector256.Create((int)MotelyItemType.BlackHole), items);
                activeMask = Vector512.AndNot(activeMask, maskBH);
            }
        }

        if (Vector512.EqualsAll(activeMask, Vector512<double>.Zero))
        {
            if (DebugLogger.IsEnabled)
                DebugLogger.Log($"[SPECTRAL] Early return - activeMask is all zeros, items={items}");
            return new MotelyItemVector(items);
        }

        // Note: We use the full ValueCount (18) here to match the single-seed implementation's PRNG behavior
        // Soul (16) and BlackHole (17) will be filtered out in the resample loop below
        Vector256<int> spectralEnums = GetNextRandomInt(ref stream.ResampleStream.InitialPrngStream, 0, MotelyEnum<MotelySpectralCard>.ValueCount, activeMask);
        Vector256<int> spectralItems = Vector256.BitwiseOr(spectralEnums, Vector256.Create((int)MotelyItemTypeCategory.SpectralCard));
        var shrunkMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(activeMask);
        items = Vector256.ConditionalSelect(shrunkMask, spectralItems, items);
        
        if (DebugLogger.IsEnabled)
            DebugLogger.Log($"[SPECTRAL] activeMask={activeMask}, shrunkMask={shrunkMask}, spectralEnums={spectralEnums}, spectralItems={spectralItems}, items={items}");

        int resampleCount = 0;
        while (true)
        {
            Vector256<int> resampleMaskInt = Vector256.Equals(items, Vector256.Create((int)MotelyItemType.Soul)) |
                                             Vector256.Equals(items, Vector256.Create((int)MotelyItemType.BlackHole));
            if (Vector256.EqualsAll(resampleMaskInt, Vector256<int>.Zero)) break;

            Vector512<double> resampleMask = MotelyVectorUtils.ExtendIntMaskToDouble(resampleMaskInt);
            Vector256<int> newEnums = GetNextRandomInt(
                ref GetResamplePrngStream(ref stream.ResampleStream, stream.ResampleKey, resampleCount),
                0, MotelyEnum<MotelySpectralCard>.ValueCount,
                resampleMask
            );
            Vector256<int> newItems = Vector256.BitwiseOr(newEnums, Vector256.Create((int)MotelyItemTypeCategory.SpectralCard));
            items = Vector256.ConditionalSelect(resampleMaskInt, newItems, items);
            ++resampleCount;
        }

        return new MotelyItemVector(items);
    }

    // Helper method for filtering spectral cards in vector context
    public VectorMask FilterSpectralCard(int ante, MotelySpectralCard targetSpectral, string source = MotelyPrngKeys.ShopItemSource)
    {
        var spectralStream = CreateSpectralStream(source, ante, true, false, true);
        var spectralChoices = MotelyEnum<MotelySpectralCard>.Values;
        var spectrals = GetNextRandomElement(ref spectralStream.ResampleStream.InitialPrngStream, spectralChoices);
        return VectorEnum256.Equals(spectrals, targetSpectral);
    }
    
    public MotelyVectorItemSet GetNextSpectralPackContents(ref MotelyVectorSpectralStream spectralStream, MotelyBoosterPackSize size)
        => GetNextSpectralPackContents(ref spectralStream, MotelyBoosterPackType.Spectral.GetCardCount(size));

    public MotelyVectorItemSet GetNextSpectralPackContents(ref MotelyVectorSpectralStream spectralStream, int size)
    {
        Debug.Assert(size <= MotelyVectorItemSet.MaxLength);

        MotelyVectorItemSet pack = new();

        for (int i = 0; i < size; i++)
            pack.Append(GetNextSpectral(ref spectralStream));

        return pack;
    }

    public VectorMask GetNextSpectralPackHasTheSoul(ref MotelyVectorSpectralStream spectralStream, MotelyBoosterPackSize size)
    {
        Debug.Assert(spectralStream.IsSoulBlackHoleable, "Spectral pack does not have the soul.");
        
        int cardCount = MotelyBoosterPackType.Spectral.GetCardCount(size);
        VectorMask hasTheSoul = VectorMask.NoBitsSet;
        VectorMask hasBlackHole = VectorMask.NoBitsSet;
        
        for (int i = 0; i < cardCount; i++)
        {
            Vector512<double> random = GetNextRandom(ref spectralStream.SoulBlackHolePrngStream);
            VectorMask isSoul = new VectorMask((uint)Vector512.ExtractMostSignificantBits(Vector512.GreaterThan(random, Vector512.Create(0.997))));
            
            if (!isSoul.IsAllFalse())
            {
                hasTheSoul |= isSoul;
                
                // Progress the stream for remaining cards
                for (; i < cardCount; i++)
                {
                    Vector512<double> randomBH = GetNextRandom(ref spectralStream.SoulBlackHolePrngStream);
                    hasBlackHole |= new VectorMask((uint)Vector512.ExtractMostSignificantBits(Vector512.GreaterThan(randomBH, Vector512.Create(0.997))));
                }
                break;
            }
            
            if (!hasBlackHole.IsAllFalse())
            {
                Vector512<double> randomBH = GetNextRandom(ref spectralStream.SoulBlackHolePrngStream);
                hasBlackHole |= new VectorMask((uint)Vector512.ExtractMostSignificantBits(Vector512.GreaterThan(randomBH, Vector512.Create(0.997))));
            }
        }
        
        return hasTheSoul;
    }

    public VectorMask GetNextSpectralPackHasThe(ref MotelyVectorSpectralStream spectralStream, MotelySpectralCard targetSpectral, MotelyBoosterPackSize size)
    {
        int cardCount = MotelyBoosterPackType.Spectral.GetCardCount(size);
        VectorMask hasTarget = VectorMask.NoBitsSet;
        
        for (int i = 0; i < cardCount; i++)
        {
            var spectral = GetNextSpectral(ref spectralStream);
            // Extract spectral card type using bit masking (similar to PlayingCardSuit pattern)
            var spectralType = new VectorEnum256<MotelySpectralCard>(Vector256.BitwiseAnd(spectral.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
            VectorMask isTarget = VectorEnum256.Equals(spectralType, targetSpectral);
            hasTarget |= isTarget;
            
            // Early exit optimization - if all lanes have found the target, no need to continue
            if (hasTarget.IsAllTrue())
                break;
        }
        
        return hasTarget;
    }

    public VectorMask GetNextSpectralPackHasThe(ref MotelyVectorSpectralStream spectralStream, MotelySpectralCard[] targetSpectrals, MotelyBoosterPackSize size)
    {
        int cardCount = MotelyBoosterPackType.Spectral.GetCardCount(size);
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        
        for (int i = 0; i < cardCount; i++)
        {
            var spectral = GetNextSpectral(ref spectralStream);
            // Extract spectral card type using bit masking (similar to PlayingCardSuit pattern)
            var spectralType = new VectorEnum256<MotelySpectralCard>(Vector256.BitwiseAnd(spectral.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
            
            VectorMask isAnyTarget = VectorMask.NoBitsSet;
            foreach (var target in targetSpectrals)
            {
                isAnyTarget |= VectorEnum256.Equals(spectralType, target);
            }
            
            hasAnyTarget |= isAnyTarget;
            
            // Early exit optimization - if all lanes have found any target, no need to continue
            if (hasAnyTarget.IsAllTrue())
                break;
        }
        
        return hasAnyTarget;
    }
}