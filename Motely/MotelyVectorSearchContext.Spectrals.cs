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
        return GetNextSpectral(ref stream, Vector512<double>.AllBitsSet);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItemVector GetNextShopSpectralOrNull(ref MotelyVectorSpectralStream spectralStream, ref MotelyVectorPrngStream itemTypeStream,
        Vector512<double> totalRate, Vector512<double> tarotRate, Vector512<double> planetRate, 
        Vector512<double> playingCardRate, Vector512<double> spectralRate)
    {
        // Check what type this slot is
        var itemTypePoll = GetNextRandom(ref itemTypeStream) * totalRate;
        itemTypePoll -= Vector512.Create(20.0); // Skip joker range
        itemTypePoll -= tarotRate; // Skip tarot range
        itemTypePoll -= planetRate; // Skip planet range  
        itemTypePoll -= playingCardRate; // Skip playing card range
        var isSpectralSlot = Vector512.LessThan(itemTypePoll, spectralRate);
        
        // Only advance spectral stream for spectral slots
        var spectral = GetNextSpectral(ref spectralStream, isSpectralSlot);
        
        // Return spectral or None for non-spectral slots
        var spectralIntMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(isSpectralSlot);
        var noneItem = Vector256<int>.Zero;
        
        return new MotelyItemVector(
            Vector256.ConditionalSelect(spectralIntMask, spectral.Value, noneItem)
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItemVector GetNextSpectral(ref MotelyVectorSpectralStream stream, in Vector512<double> mask)
    {
        if (stream.IsNull) 
        {
            if (DebugLogger.IsEnabled)
                DebugLogger.Log($"[SPECTRAL] Stream is null! ResampleKey={stream.ResampleKey}");
            return new MotelyItemVector(Vector256<int>.Zero);
        }

        Vector256<int> items = Vector256<int>.Zero;
        Vector512<double> activeMask = mask;
        
        if (DebugLogger.IsEnabled)
            DebugLogger.Log($"[SPECTRAL] Initial activeMask={activeMask}");

        if (stream.IsSoulBlackHoleable)
        {
            Vector512<double> randomSoul = GetNextRandom(ref stream.SoulBlackHolePrngStream, activeMask);
            Vector512<double> maskSoul = activeMask & Vector512.GreaterThan(randomSoul, Vector512.Create(0.997));
            Vector256<int> maskSoulInt = MotelyVectorUtils.ShrinkDoubleMaskToInt(maskSoul);
            items = Vector256.ConditionalSelect(maskSoulInt, Vector256.Create((int)MotelyItemType.Soul), items);
            activeMask = Vector512.AndNot(activeMask, maskSoul);

            if (!Vector512.EqualsAll(activeMask, Vector512<double>.Zero))
            {
                Vector512<double> randomBH = GetNextRandom(ref stream.SoulBlackHolePrngStream, activeMask);
                Vector512<double> maskBH = activeMask & Vector512.GreaterThan(randomBH, Vector512.Create(0.997));
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

    public MotelyVectorItemSet GetNextSpectralPackContentsPerLane(ref MotelyVectorSpectralStream spectralStream, VectorEnum256<MotelyBoosterPackSize> packSizes, VectorMask isSpectralPack)
    {
        MotelyVectorItemSet pack = new();

        // Create masks for different pack sizes
        VectorMask isNormalSize = VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Normal);   // 2 cards
        VectorMask isJumboSize = VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Jumbo);    // 4 cards
        VectorMask isMegaSize = VectorEnum256.Equals(packSizes, MotelyBoosterPackSize.Mega);      // 4 cards

        // Pre-allocate mask elements buffer outside the loop to avoid stack overflow warning
        Span<int> maskElements = stackalloc int[8];

        // Always generate the maximum possible cards (5) to maintain stream synchronization
        // But mask out results for lanes that shouldn't use certain card positions
        for (int cardIndex = 0; cardIndex < MotelyVectorItemSet.MaxLength; cardIndex++)
        {
            var spectralCard = GetNextSpectral(ref spectralStream);
            
            // Create mask for lanes where this card position should be valid
            VectorMask shouldIncludeCard = cardIndex switch
            {
                0 or 1 => VectorMask.AllBitsSet, // All pack sizes have at least 2 cards
                2 or 3 => VectorMask.AllBitsSet ^ isNormalSize,  // Jumbo and Mega have 3rd and 4th cards (NOT Normal)
                4 => VectorMask.NoBitsSet,       // No pack size has a 5th card
                _ => VectorMask.NoBitsSet
            };

            // Apply mask to spectral card - for lanes where card shouldn't exist, 
            // we still generated it (for stream sync) but mark it as invalid
            if (!shouldIncludeCard.IsAllTrue())
            {
                // Create conditional selection masks manually using bit operations
                for (int i = 0; i < 8; i++)
                {
                    maskElements[i] = shouldIncludeCard[i] ? -1 : 0; // -1 = all bits set, 0 = no bits set
                }
                var selectionMask = Vector256.Create<int>(maskElements);
                
                // For invalid lanes, use the special marker value
                var invalidType = Vector256.Create((int)MotelyItemType.SpectralExcludedByStream);
                var maskedType = Vector256.ConditionalSelect(selectionMask, spectralCard.Type.HardwareVector, invalidType);
                
                // Create new spectral card with the masked type
                spectralCard = new MotelyItemVector(maskedType);
            }

            pack.Append(spectralCard);
        }

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