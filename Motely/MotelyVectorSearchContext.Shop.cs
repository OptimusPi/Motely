using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public ref struct MotelyVectorShopItemStream
{
    public MotelyVectorPrngStream ItemTypeStream;
    public MotelyVectorJokerStream JokerStream;
    public MotelyVectorTarotStream TarotStream;
    public MotelyVectorPlanetStream PlanetStream;
    public MotelyVectorSpectralStream SpectralStream;
    
    public Vector512<double> TarotRate;
    public Vector512<double> PlanetRate;
    public Vector512<double> PlayingCardRate;
    public Vector512<double> SpectralRate;
    public Vector512<double> TotalRate;
    
    public readonly bool DoesProvideJokers => !JokerStream.IsNull;
    public readonly bool DoesProvideTarots => !TarotStream.IsNull;
    public readonly bool DoesProvidePlanets => !PlanetStream.IsNull;
    public readonly bool DoesProvideSpectrals => !SpectralStream.IsNull;
}

ref partial struct MotelyVectorSearchContext
{
    private const int ShopJokerRate = 20;

    public MotelyVectorShopItemStream CreateShopItemStream(int ante, 
        MotelyShopStreamFlags flags = MotelyShopStreamFlags.Default,
        MotelyJokerStreamFlags jokerFlags = MotelyJokerStreamFlags.Default,
        bool isCached = false)
    {
        return CreateShopItemStream(ante, Deck.GetDefaultRunState(), flags, jokerFlags, isCached);
    }
    
    public MotelyVectorShopItemStream CreateShopItemStream(int ante,
        MotelyRunState runState,
        MotelyShopStreamFlags flags = MotelyShopStreamFlags.Default,
        MotelyJokerStreamFlags jokerFlags = MotelyJokerStreamFlags.Default,
        bool isCached = false)
    {
        MotelyVectorShopItemStream stream = new()
        {
            ItemTypeStream = CreatePrngStream(MotelyPrngKeys.ShopItemType + ante, isCached),
            JokerStream = flags.HasFlag(MotelyShopStreamFlags.ExcludeJokers) ?
                default : CreateShopJokerStream(ante, jokerFlags, isCached),
            TarotStream = flags.HasFlag(MotelyShopStreamFlags.ExcludeTarots) ?
                default : CreateShopTarotStream(ante, isCached),
            PlanetStream = flags.HasFlag(MotelyShopStreamFlags.ExcludePlanets) ?
                default : CreateShopPlanetStream(ante, isCached),
            SpectralStream = flags.HasFlag(MotelyShopStreamFlags.ExcludeSpectrals) || Deck != MotelyDeck.Ghost ?
                default : CreateShopSpectralStream(ante, isCached),
                
            TarotRate = Vector512.Create(4.0),
            PlanetRate = Vector512.Create(4.0),
            PlayingCardRate = Vector512.Create(0.0),
            SpectralRate = Vector512.Create(0.0)
        };
        
        if (Deck == MotelyDeck.Ghost)
        {
            stream.SpectralRate = Vector512.Create(2.0);
        }
        
        if (runState.IsVoucherActive(MotelyVoucher.TarotTycoon))
        {
            stream.TarotRate = Vector512.Create(32.0);
        }
        else if (runState.IsVoucherActive(MotelyVoucher.TarotMerchant))
        {
            stream.TarotRate = Vector512.Create(9.6);
        }
        
        if (runState.IsVoucherActive(MotelyVoucher.PlanetTycoon))
        {
            stream.PlanetRate = Vector512.Create(32.0);
        }
        else if (runState.IsVoucherActive(MotelyVoucher.PlanetMerchant))
        {
            stream.PlanetRate = Vector512.Create(9.6);
        }
        
        if (runState.IsVoucherActive(MotelyVoucher.MagicTrick))
        {
            stream.PlayingCardRate = Vector512.Create(4.0);
        }
        
        stream.TotalRate = Vector512.Create((double)ShopJokerRate) + stream.TarotRate + stream.PlanetRate + stream.PlayingCardRate + stream.SpectralRate;
        
        return stream;
    }
    
#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public VectorEnum256<MotelyItemTypeCategory> GetNextShopSlotType(ref MotelyVectorShopItemStream stream, Vector512<double> mask)
    {
        Vector512<double> itemTypePoll = GetNextRandom(ref stream.ItemTypeStream, mask) * stream.TotalRate;
        Vector512<double> shopJokerRateVector = Vector512.Create((double)ShopJokerRate);
        
        // Determine item type for each lane
        Vector512<double> isJoker = Vector512.LessThan(itemTypePoll, shopJokerRateVector);
        itemTypePoll -= shopJokerRateVector;
        
        Vector512<double> isTarot = Vector512.LessThan(itemTypePoll, stream.TarotRate);
        itemTypePoll -= stream.TarotRate;
        
        Vector512<double> isPlanet = Vector512.LessThan(itemTypePoll, stream.PlanetRate);
        itemTypePoll -= stream.PlanetRate;
        
        Vector512<double> isPlayingCard = Vector512.LessThan(itemTypePoll, stream.PlayingCardRate);
        
        // Build the result - what type category is in this slot
        var result = Vector256.ConditionalSelect(
            MotelyVectorUtils.ShrinkDoubleMaskToInt(isJoker), 
            Vector256.Create((int)MotelyItemTypeCategory.Joker),
            Vector256.ConditionalSelect(
                MotelyVectorUtils.ShrinkDoubleMaskToInt(isTarot),
                Vector256.Create((int)MotelyItemTypeCategory.TarotCard),
                Vector256.ConditionalSelect(
                    MotelyVectorUtils.ShrinkDoubleMaskToInt(isPlanet),
                    Vector256.Create((int)MotelyItemTypeCategory.PlanetCard),
                    Vector256.ConditionalSelect(
                        MotelyVectorUtils.ShrinkDoubleMaskToInt(isPlayingCard),
                        Vector256.Create((int)MotelyItemTypeCategory.PlayingCard),
                        Vector256.Create((int)MotelyItemTypeCategory.SpectralCard)))));
        
        return new VectorEnum256<MotelyItemTypeCategory>(result);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItemVector GetNextShopItem(ref MotelyVectorShopItemStream stream)
    {
        // Get the type for all lanes (advances ItemTypeStream for all)
        var slotType = GetNextShopSlotType(ref stream, Vector512<double>.AllBitsSet);
        
        // Now pull from the appropriate stream based on type
        // We need to pull from ALL streams to keep them in sync
        MotelyItemVector jokerItem = stream.DoesProvideJokers ? 
            GetNextJoker(ref stream.JokerStream) : 
            new MotelyItemVector(new MotelyItem(MotelyItemType.JokerExcludedByStream));
            
        MotelyItemVector tarotItem = stream.DoesProvideTarots ?
            GetNextTarot(ref stream.TarotStream) :
            new MotelyItemVector(new MotelyItem(MotelyItemType.TarotExcludedByStream));
            
        MotelyItemVector planetItem = stream.DoesProvidePlanets ?
            GetNextPlanet(ref stream.PlanetStream) :
            new MotelyItemVector(new MotelyItem(MotelyItemType.PlanetExcludedByStream));
            
        MotelyItemVector playingCardItem = new MotelyItemVector(new MotelyItem(MotelyItemType.NotImplemented));
        
        MotelyItemVector spectralItem = stream.DoesProvideSpectrals ?
            GetNextSpectral(ref stream.SpectralStream) :
            new MotelyItemVector(new MotelyItem(MotelyItemType.SpectralExcludedByStream));
        
        // Select based on the type
        var isJoker = Vector256.Equals(slotType.HardwareVector, Vector256.Create((int)MotelyItemTypeCategory.Joker));
        var isTarot = Vector256.Equals(slotType.HardwareVector, Vector256.Create((int)MotelyItemTypeCategory.TarotCard));
        var isPlanet = Vector256.Equals(slotType.HardwareVector, Vector256.Create((int)MotelyItemTypeCategory.PlanetCard));
        var isPlayingCard = Vector256.Equals(slotType.HardwareVector, Vector256.Create((int)MotelyItemTypeCategory.PlayingCard));
        
        MotelyItemVector result = new MotelyItemVector(Vector256.ConditionalSelect(
            isJoker, jokerItem.Value,
            Vector256.ConditionalSelect(
                isTarot, tarotItem.Value,
                Vector256.ConditionalSelect(
                    isPlanet, planetItem.Value,
                    Vector256.ConditionalSelect(
                        isPlayingCard, playingCardItem.Value,
                        spectralItem.Value)))));
        
        return result;
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyItemType targetType, int slotCount)
    {
        VectorMask hasTarget = VectorMask.NoBitsSet;
        
        for (int i = 0; i < slotCount; i++)
        {
            var item = GetNextShopItem(ref stream);
            VectorMask isTarget = VectorEnum256.Equals(item.Type, targetType);
            hasTarget |= isTarget;
            
            // Early exit optimization - if all lanes have found the target, no need to continue
            if (hasTarget.IsAllTrue())
                break;
        }
        
        return hasTarget;
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyItemType[] targetTypes, int slotCount)
    {
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        
        for (int i = 0; i < slotCount; i++)
        {
            var item = GetNextShopItem(ref stream);
            var itemType = item.Type;
            
            VectorMask isAnyTarget = VectorMask.NoBitsSet;
            foreach (var target in targetTypes)
            {
                isAnyTarget |= VectorEnum256.Equals(itemType, target);
            }
            
            hasAnyTarget |= isAnyTarget;
            
            // Early exit optimization - if all lanes have found any target, no need to continue
            if (hasAnyTarget.IsAllTrue())
                break;
        }
        
        return hasAnyTarget;
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyJoker targetJoker)
    {
        var item = GetNextShopItem(ref stream);
        // Extract joker card type using bit masking
        var jokerType = new VectorEnum256<MotelyJoker>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        return VectorEnum256.Equals(jokerType, targetJoker);
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyTarotCard targetTarot)
    {
        var item = GetNextShopItem(ref stream);
        // Extract tarot card type using bit masking
        var tarotType = new VectorEnum256<MotelyTarotCard>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        return VectorEnum256.Equals(tarotType, targetTarot);
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyPlanetCard targetPlanet)
    {
        var item = GetNextShopItem(ref stream);
        // Extract planet card type using bit masking
        var planetType = new VectorEnum256<MotelyPlanetCard>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        return VectorEnum256.Equals(planetType, targetPlanet);
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelySpectralCard targetSpectral)
    {
        var item = GetNextShopItem(ref stream);
        // Extract spectral card type using bit masking
        var spectralType = new VectorEnum256<MotelySpectralCard>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        return VectorEnum256.Equals(spectralType, targetSpectral);
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyPlayingCard targetCard)
    {
        var item = GetNextShopItem(ref stream);
        // Extract playing card type using bit masking
        var cardType = new VectorEnum256<MotelyPlayingCard>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        return VectorEnum256.Equals(cardType, targetCard);
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyItemType targetItemType)
    {
        var item = GetNextShopItem(ref stream);
        return VectorEnum256.Equals(new VectorEnum256<MotelyItemType>(item.Value), targetItemType);
    }

    // Array versions for checking multiple targets
    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyJoker[] targetJokers)
    {
        var item = GetNextShopItem(ref stream);
        var jokerType = new VectorEnum256<MotelyJoker>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        foreach (var target in targetJokers)
        {
            hasAnyTarget |= VectorEnum256.Equals(jokerType, target);
        }
        return hasAnyTarget;
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyTarotCard[] targetTarots)
    {
        var item = GetNextShopItem(ref stream);
        var tarotType = new VectorEnum256<MotelyTarotCard>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        foreach (var target in targetTarots)
        {
            hasAnyTarget |= VectorEnum256.Equals(tarotType, target);
        }
        return hasAnyTarget;
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyPlanetCard[] targetPlanets)
    {
        var item = GetNextShopItem(ref stream);
        var planetType = new VectorEnum256<MotelyPlanetCard>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        foreach (var target in targetPlanets)
        {
            hasAnyTarget |= VectorEnum256.Equals(planetType, target);
        }
        return hasAnyTarget;
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelySpectralCard[] targetSpectrals)
    {
        var item = GetNextShopItem(ref stream);
        var spectralType = new VectorEnum256<MotelySpectralCard>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        foreach (var target in targetSpectrals)
        {
            hasAnyTarget |= VectorEnum256.Equals(spectralType, target);
        }
        return hasAnyTarget;
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyPlayingCard[] targetCards)
    {
        var item = GetNextShopItem(ref stream);
        var cardType = new VectorEnum256<MotelyPlayingCard>(Vector256.BitwiseAnd(item.Value, Vector256.Create(Motely.ItemTypeMask & ~Motely.ItemTypeCategoryMask)));
        
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        foreach (var target in targetCards)
        {
            hasAnyTarget |= VectorEnum256.Equals(cardType, target);
        }
        return hasAnyTarget;
    }

    public VectorMask GetNextShopSlotHasThe(ref MotelyVectorShopItemStream stream, MotelyItemType[] targetItemTypes)
    {
        var item = GetNextShopItem(ref stream);
        var itemType = new VectorEnum256<MotelyItemType>(item.Value);
        
        VectorMask hasAnyTarget = VectorMask.NoBitsSet;
        foreach (var target in targetItemTypes)
        {
            hasAnyTarget |= VectorEnum256.Equals(itemType, target);
        }
        return hasAnyTarget;
    }

    /// <summary>
    /// Centralized shop type detection - gets item type categories for all shop slots
    /// This eliminates redundant shop stream creation across multiple filters
    /// </summary>
    public VectorEnum256<MotelyItemTypeCategory>[] GetShopSlotTypes(int ante, int maxSlots, 
        MotelyShopStreamFlags flags = MotelyShopStreamFlags.Default)
    {
        var shopStream = CreateShopItemStream(ante, flags);
        var shopTypes = new VectorEnum256<MotelyItemTypeCategory>[maxSlots];
        
        for (int slot = 0; slot < maxSlots; slot++)
        {
            shopTypes[slot] = GetNextShopSlotType(ref shopStream, Vector512<double>.AllBitsSet);
        }
        
        return shopTypes;
    }

    /// <summary>
    /// Gets full shop items for specific slots after type detection
    /// Use this after GetShopSlotTypes() to get detailed item info only for relevant slots
    /// </summary>
    public MotelyItemVector[] GetShopItems(int ante, int maxSlots,
        MotelyShopStreamFlags flags = MotelyShopStreamFlags.Default)
    {
        var shopStream = CreateShopItemStream(ante, flags);
        var shopItems = new MotelyItemVector[maxSlots];
        
        for (int slot = 0; slot < maxSlots; slot++)
        {
            shopItems[slot] = GetNextShopItem(ref shopStream);
        }
        
        return shopItems;
    }
}