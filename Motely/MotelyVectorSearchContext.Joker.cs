
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;


public ref struct MotelyVectorJokerStream
{
    public readonly bool IsNull => StreamSuffix == null;

    public string StreamSuffix;
    public MotelyVectorPrngStream EditionPrngStream;
    public MotelyVectorPrngStream RarityPrngStream;
    public MotelyVectorPrngStream EternalPerishablePrngStream;
    public MotelyVectorPrngStream RentalPrngStream;

    // For these, a state set to -1 means they are not yet initialized.
    //  A state of -2 means the stream does not provide that joker
    public MotelyVectorPrngStream CommonJokerPrngStream;
    public MotelyVectorPrngStream UncommonJokerPrngStream;
    public MotelyVectorPrngStream RareJokerPrngStream;

    public readonly bool DoesProvideCommonJokers => !CommonJokerPrngStream.IsInvalid;
    public readonly bool DoesProvideUncommonJokers => !UncommonJokerPrngStream.IsInvalid;
    public readonly bool DoesProvideRareJokers => !RareJokerPrngStream.IsInvalid;
    public readonly bool DoesProvideEdition => !EditionPrngStream.IsInvalid;
    public readonly bool DoesProvideStickers => !EternalPerishablePrngStream.IsInvalid;
}

public struct MotelyVectorJokerFixedRarityStream
{
    public MotelyJokerRarity Rarity;
    public MotelyVectorPrngStream EditionPrngStream;
    public MotelyVectorPrngStream EternalPerishablePrngStream;
    public MotelyVectorPrngStream RentalPrngStream;
    public MotelyVectorPrngStream JokerPrngStream;

    public readonly bool DoesProvideEdition => !EditionPrngStream.IsInvalid;
    public readonly bool DoesProvideStickers => !EternalPerishablePrngStream.IsInvalid;
}

unsafe partial struct MotelyVectorSearchContext
{

    public MotelyVectorJokerStream CreateShopJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false)
    {
        return CreateJokerStream(
            MotelyPrngKeys.ShopItemSource,
            MotelyPrngKeys.ShopJokerEternalPerishableSource,
            MotelyPrngKeys.ShopJokerRentalSource,
            ante, flags, isCached
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelyVectorJokerStream CreateJokerStream(string source, string eternalPerishableSource, string rentalSource, int ante, MotelyJokerStreamFlags flags, bool isCached)
    {
        string streamSuffix = source + ante;

        return new()
        {
            StreamSuffix = streamSuffix,
            RarityPrngStream = CreatePrngStream(MotelyPrngKeys.JokerRarity + ante + source, isCached),
            EditionPrngStream = !flags.HasFlag(MotelyJokerStreamFlags.ExcludeEdition) ?
                CreatePrngStream(MotelyPrngKeys.JokerEdition + streamSuffix, isCached) : MotelyVectorPrngStream.Invalid,
            EternalPerishablePrngStream = (!flags.HasFlag(MotelyJokerStreamFlags.ExcludeStickers) && Stake >= MotelyStake.Black) ?
                CreatePrngStream(eternalPerishableSource + ante, isCached) : MotelyVectorPrngStream.Invalid,
            RentalPrngStream = (!flags.HasFlag(MotelyJokerStreamFlags.ExcludeStickers) && Stake >= MotelyStake.Gold) ?
                CreatePrngStream(rentalSource + ante, isCached) : MotelyVectorPrngStream.Invalid,
            CommonJokerPrngStream = !flags.HasFlag(MotelyJokerStreamFlags.ExcludeCommonJokers) ?
                CreatePrngStream(MotelyPrngKeys.JokerCommon + streamSuffix, isCached) : MotelyVectorPrngStream.Invalid,
            UncommonJokerPrngStream = !flags.HasFlag(MotelyJokerStreamFlags.ExcludeUncommonJokers) ?
                CreatePrngStream(MotelyPrngKeys.JokerUncommon + streamSuffix, isCached) : MotelyVectorPrngStream.Invalid,
            RareJokerPrngStream = !flags.HasFlag(MotelyJokerStreamFlags.ExcludeRareJokers) ?
                CreatePrngStream(MotelyPrngKeys.JokerRare + streamSuffix, isCached) : MotelyVectorPrngStream.Invalid,
        };
    }

    public MotelyVectorJokerStream CreateBuffoonPackJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false)
    {
        // Single stream per ante (not per pack index)
        return CreateJokerStream(
            MotelyPrngKeys.BuffoonPackItemSource,
            MotelyPrngKeys.BuffoonJokerEternalPerishableSource,
            MotelyPrngKeys.BuffoonJokerRentalSource,
            ante, flags, isCached
        );
    }

    public MotelyVectorJokerFixedRarityStream CreateSoulJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false)
    {
        return CreateJokerFixedRarityStream(
            MotelyPrngKeys.JokerSoulSource,
            MotelyPrngKeys.ShopJokerEternalPerishableSource,
            MotelyPrngKeys.ShopJokerRentalSource,
            ante, flags, MotelyJokerRarity.Legendary, isCached
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelyVectorJokerFixedRarityStream CreateJokerFixedRarityStream(string source, string eternalPerishableSource, string rentalSource, int ante, MotelyJokerStreamFlags flags, MotelyJokerRarity rarity, bool isCached)
    {
        return new()
        {
            Rarity = rarity,
            JokerPrngStream = CreatePrngStream(MotelyPrngKeys.FixedRarityJoker(rarity, source, ante), isCached),
            EditionPrngStream = !flags.HasFlag(MotelyJokerStreamFlags.ExcludeEdition) ?
                CreatePrngStream(MotelyPrngKeys.JokerEdition + source + ante, isCached) : MotelyVectorPrngStream.Invalid,
            EternalPerishablePrngStream = (!flags.HasFlag(MotelyJokerStreamFlags.ExcludeStickers) && Stake >= MotelyStake.Black) ?
                CreatePrngStream(eternalPerishableSource + ante, isCached) : MotelyVectorPrngStream.Invalid,
            RentalPrngStream = (!flags.HasFlag(MotelyJokerStreamFlags.ExcludeStickers) && Stake >= MotelyStake.Gold) ?
                CreatePrngStream(rentalSource + ante, isCached) : MotelyVectorPrngStream.Invalid,
        };
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private VectorEnum256<MotelyItemEdition> GetNextEdition(ref MotelyVectorPrngStream stream, int editionRate)
    {
        Vector512<double> editionPoll = GetNextRandom(ref stream);

        // O_O
        return new(
            Vector256.ConditionalSelect(
                MotelyVectorUtils.ShrinkDoubleMaskToInt(Vector512.GreaterThan(editionPoll, Vector512.Create(0.997))),
                Vector256.Create((int)MotelyItemEdition.Negative),
            Vector256.ConditionalSelect(
                MotelyVectorUtils.ShrinkDoubleMaskToInt(Vector512.GreaterThan(editionPoll, Vector512.Create(1 - 0.006 * editionRate))),
                Vector256.Create((int)MotelyItemEdition.Polychrome),
            Vector256.ConditionalSelect(
                MotelyVectorUtils.ShrinkDoubleMaskToInt(Vector512.GreaterThan(editionPoll, Vector512.Create(1 - 0.02 * editionRate))),
                Vector256.Create((int)MotelyItemEdition.Holographic),
            Vector256.ConditionalSelect(
                MotelyVectorUtils.ShrinkDoubleMaskToInt(Vector512.GreaterThan(editionPoll, Vector512.Create(1 - 0.04 * editionRate))),
                Vector256.Create((int)MotelyItemEdition.Foil),
                Vector256.Create((int)MotelyItemEdition.None)
        )))));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelyItemVector ApplyNextStickers(MotelyItemVector item, ref MotelyVectorPrngStream eternalPerishableStream, ref MotelyVectorPrngStream rentalStream)
    {
        if (Stake < MotelyStake.Black) return item;

        Debug.Assert(!eternalPerishableStream.IsInvalid);

        Vector512<double> stickerPoll = GetNextRandom(ref eternalPerishableStream);

        Vector256<int> eternalMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(Vector512.GreaterThan(stickerPoll, Vector512.Create(0.7)));
        
        // Mask out jokers that cannot be eternal (self-destruct or activate on sell)
        var cannotBeEternalMask = 
            VectorEnum256.Equals(item.Type, MotelyItemType.Cavendish) |
            VectorEnum256.Equals(item.Type, MotelyItemType.DietCola) |
            VectorEnum256.Equals(item.Type, MotelyItemType.GrosMichel) |
            VectorEnum256.Equals(item.Type, MotelyItemType.IceCream) |
            VectorEnum256.Equals(item.Type, MotelyItemType.InvisibleJoker) |
            VectorEnum256.Equals(item.Type, MotelyItemType.Luchador) |
            VectorEnum256.Equals(item.Type, MotelyItemType.MrBones) |
            VectorEnum256.Equals(item.Type, MotelyItemType.Popcorn) |
            VectorEnum256.Equals(item.Type, MotelyItemType.Ramen) |
            VectorEnum256.Equals(item.Type, MotelyItemType.Seltzer) |
            VectorEnum256.Equals(item.Type, MotelyItemType.TurtleBean);
        
        // Only apply eternal to jokers that can be eternal
        eternalMask &= ~cannotBeEternalMask;
        item = item.WithEternal(eternalMask);

        if (Stake < MotelyStake.Orange) return item;

        // Only apply perishable if not eternal
        Vector256<int> perishableMask = ~eternalMask & MotelyVectorUtils.ShrinkDoubleMaskToInt(Vector512.GreaterThan(stickerPoll, Vector512.Create(0.4)));
        item = item.WithPerishable(perishableMask);

        if (Stake < MotelyStake.Gold) return item;

        Debug.Assert(!rentalStream.IsInvalid);

        stickerPoll = GetNextRandom(ref rentalStream);

        Vector256<int> rentallMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(Vector512.GreaterThan(stickerPoll, Vector512.Create(0.7)));
        item = item.WithRental(rentallMask);

        return item;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItemVector GetNextShopJokerOrNull(ref MotelyVectorJokerStream jokerStream, ref MotelyVectorPrngStream itemTypeStream, Vector512<double> totalRate)
    {
        // Check what type this slot is
        var itemTypePoll = GetNextRandom(ref itemTypeStream) * totalRate;
        var isJokerSlot = Vector512.LessThan(itemTypePoll, Vector512.Create(20.0)); // ShopJokerRate
        
        // Only advance joker stream for joker slots
        var joker = GetNextJoker(ref jokerStream, isJokerSlot);
        
        // Return joker or None for non-joker slots
        var jokerIntMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(isJokerSlot);
        var noneItem = Vector256<int>.Zero;
        
        return new MotelyItemVector(
            Vector256.ConditionalSelect(jokerIntMask, joker.Value, noneItem)
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItemVector GetNextJoker(ref MotelyVectorJokerFixedRarityStream stream)
    {

        MotelyItemVector item;

        switch (stream.Rarity)
        {
            case MotelyJokerRarity.Legendary:
                item = new(GetNextJoker<MotelyJokerLegendary>(ref stream.JokerPrngStream, MotelyJokerRarity.Legendary));
                break;
            case MotelyJokerRarity.Rare:
                item = new(GetNextJoker<MotelyJokerRare>(ref stream.JokerPrngStream, MotelyJokerRarity.Rare));
                break;
            case MotelyJokerRarity.Uncommon:
                item = new(GetNextJoker<MotelyJokerUncommon>(ref stream.JokerPrngStream, MotelyJokerRarity.Uncommon));
                break;
            default:
                Debug.Assert(stream.Rarity == MotelyJokerRarity.Common);
                item = new(GetNextJoker<MotelyJokerCommon>(ref stream.JokerPrngStream, MotelyJokerRarity.Common));
                break;
        }

        if (stream.DoesProvideEdition)
        {
            item = item.WithEdition(GetNextEdition(ref stream.EditionPrngStream, 1));
        }

        if (stream.DoesProvideStickers)
        {
            item = ApplyNextStickers(item, ref stream.EternalPerishablePrngStream, ref stream.RentalPrngStream);
        }

        return item;
    }

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItemVector GetNextJoker(ref MotelyVectorJokerStream stream)
    {
        return GetNextJoker(ref stream, Vector512<double>.AllBitsSet);
    }

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItemVector GetNextJoker(ref MotelyVectorJokerStream stream, Vector512<double> mask)
    {

        MotelyItemVector jokers;

        // Pick the joker
        {
            Vector512<double> rarityPoll = GetNextRandom(ref stream.RarityPrngStream, mask);

            Vector512<double> rareMask = Vector512.GreaterThan(rarityPoll, Vector512.Create(0.95)) & mask;
            Vector512<double> uncommonMask = ~rareMask & Vector512.GreaterThan(rarityPoll, Vector512.Create(0.7)) & mask;
            Vector512<double> commonMask = ~rareMask & ~uncommonMask & mask;

            Vector256<int> rareJokers = stream.DoesProvideRareJokers ?
                GetNextJoker<MotelyJokerRare>(ref stream.RareJokerPrngStream, MotelyJokerRarity.Rare, rareMask) :
                Vector256.Create(new MotelyItem(MotelyItemType.JokerExcludedByStream).Value);

            Vector256<int> uncommonJokers = stream.DoesProvideUncommonJokers ?
                GetNextJoker<MotelyJokerUncommon>(ref stream.UncommonJokerPrngStream, MotelyJokerRarity.Uncommon, uncommonMask) :
                Vector256.Create(new MotelyItem(MotelyItemType.JokerExcludedByStream).Value);

            Vector256<int> commonJokers = stream.DoesProvideCommonJokers ?
                GetNextJoker<MotelyJokerCommon>(ref stream.CommonJokerPrngStream, MotelyJokerRarity.Common, commonMask) :
                Vector256.Create(new MotelyItem(MotelyItemType.JokerExcludedByStream).Value);

            jokers = new(Vector256.Create((int)MotelyItemTypeCategory.Joker) | Vector256.ConditionalSelect(
                MotelyVectorUtils.ShrinkDoubleMaskToInt(rareMask),
                rareJokers,
                Vector256.ConditionalSelect(
                    MotelyVectorUtils.ShrinkDoubleMaskToInt(uncommonMask),
                    uncommonJokers, commonJokers
                )
            ));
        }

        if (stream.DoesProvideEdition)
        {
            jokers = new(jokers.Value | GetNextEdition(ref stream.EditionPrngStream, 1).HardwareVector);
        }

        if (stream.DoesProvideStickers)
        {
            jokers = ApplyNextStickers(jokers, ref stream.EternalPerishablePrngStream, ref stream.RentalPrngStream);
        }

        return jokers;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private Vector256<int> GetNextJoker<T>(ref MotelyVectorPrngStream stream, MotelyJokerRarity rarity, Vector512<double> mask) where T : unmanaged, Enum
    {
        Debug.Assert(sizeof(T) == 4);
        return Vector256.BitwiseOr(Vector256.Create((int)rarity), GetNextRandomInt(ref stream, 0, MotelyEnum<T>.ValueCount, mask));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private Vector256<int> GetNextJoker<T>(ref MotelyVectorPrngStream stream, MotelyJokerRarity rarity) where T : unmanaged, Enum
    {
        Debug.Assert(sizeof(T) == 4);
        return Vector256.BitwiseOr(Vector256.Create((int)rarity), GetNextRandomInt(ref stream, 0, MotelyEnum<T>.ValueCount));
    }

    public MotelyVectorItemSet GetNextBuffoonPackContents(ref MotelyVectorJokerStream jokerStream, MotelyBoosterPackSize size)
        => GetNextBuffoonPackContents(ref jokerStream, MotelyBoosterPackType.Buffoon.GetCardCount(size));

    public MotelyVectorItemSet GetNextBuffoonPackContents(ref MotelyVectorJokerStream jokerStream, int size)
    {
        Debug.Assert(size <= MotelyVectorItemSet.MaxLength);

        MotelyVectorItemSet pack = new();

        for (int i = 0; i < size; i++)
            pack.Append(GetNextJoker(ref jokerStream)); // Duplicate handling in upstream resampling system

        return pack;
    }
    
    /// <summary>
    /// Gets buffoon pack contents with masked PRNG advancement - only valid lanes advance their streams.
    /// Uses OrNull pattern to ensure PRNG synchronization in partial batches.
    /// </summary>
    public MotelyVectorItemSet GetNextBuffoonPackContentsMasked(ref MotelyVectorJokerStream jokerStream, MotelyBoosterPackSize size, Vector512<double> validLanesMask)
        => GetNextBuffoonPackContentsMasked(ref jokerStream, MotelyBoosterPackType.Buffoon.GetCardCount(size), validLanesMask);
    
    public MotelyVectorItemSet GetNextBuffoonPackContentsMasked(ref MotelyVectorJokerStream jokerStream, int size, Vector512<double> validLanesMask)
    {
        Debug.Assert(size <= MotelyVectorItemSet.MaxLength);

        MotelyVectorItemSet pack = new();
        
        // Convert double mask to int mask for conditional selection
        var validIntMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(validLanesMask);
        var noneItem = Vector256<int>.Zero;

        for (int i = 0; i < size; i++)
        {
            // Only advance PRNG for valid lanes
            var joker = GetNextJoker(ref jokerStream, validLanesMask);
            
            // Use ConditionalSelect: valid lanes get joker, invalid lanes get None
            var maskedJoker = new MotelyItemVector(
                Vector256.ConditionalSelect(validIntMask, joker.Value, noneItem)
            );
            
            pack.Append(maskedJoker);
        }

        return pack;
    }
}