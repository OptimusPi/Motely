
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely;

public struct MotelySingleJokerStream
{
    public readonly bool IsNull => StreamSuffix == null;

    public string StreamSuffix;
    public MotelySinglePrngStream EditionPrngStream;
    public MotelySinglePrngStream RarityPrngStream;
    public MotelySinglePrngStream EternalPerishablePrngStream;
    public MotelySinglePrngStream RentalPrngStream;
    public string ResampleKey; // Key to create resample stream for handling duplicates when needed

    // For these, a state set to -1 means they are not yet initialized.
    //  A state of -2 means the stream does not provide that joker
    public MotelySinglePrngStream CommonJokerPrngStream;
    public MotelySinglePrngStream UncommonJokerPrngStream;
    public MotelySinglePrngStream RareJokerPrngStream;

    public readonly bool DoesProvideCommonJokers => CommonJokerPrngStream.State != -2;
    public readonly bool DoesProvideUncommonJokers => UncommonJokerPrngStream.State != -2;
    public readonly bool DoesProvideRareJokers => RareJokerPrngStream.State != -2;
    public readonly bool DoesProvideEdition => !EditionPrngStream.IsInvalid;
    public readonly bool DoesProvideStickers => !EternalPerishablePrngStream.IsInvalid;
}

public struct MotelySingleJokerFixedRarityStream
{
    public MotelyJokerRarity Rarity;
    public MotelySinglePrngStream EditionPrngStream;
    public MotelySinglePrngStream EternalPerishablePrngStream;
    public MotelySinglePrngStream RentalPrngStream;
    public MotelySinglePrngStream JokerPrngStream;

    public readonly bool DoesProvideEdition => !EditionPrngStream.IsInvalid;
    public readonly bool DoesProvideStickers => !EternalPerishablePrngStream.IsInvalid;
}

[Flags]
public enum MotelyJokerStreamFlags
{
    ExcludeStickers = 1 << 1,
    ExcludeEdition = 1 << 2,

    ExcludeCommonJokers = 1 << 3,
    ExcludeUncommonJokers = 1 << 4,
    ExcludeRareJokers = 1 << 5,

    Default = 0
}

unsafe ref partial struct MotelySingleSearchContext
{

    public MotelySingleJokerStream CreateShopJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false)
    {
        return CreateJokerStream(
            MotelyPrngKeys.ShopItemSource,
            MotelyPrngKeys.ShopJokerEternalPerishableSource,
            MotelyPrngKeys.ShopJokerRentalSource,
            ante, flags, isCached
        );
    }

    public MotelySingleJokerStream CreateBuffoonPackJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false)
    {
        // Single stream per ante (not per pack index)
        // Include resample stream for handling duplicates in buffoon packs
        return CreateJokerStream(
            MotelyPrngKeys.BuffoonPackItemSource,
            MotelyPrngKeys.BuffoonJokerEternalPerishableSource,
            MotelyPrngKeys.BuffoonJokerRentalSource,
            ante, flags, isCached, includeResampleStream: true
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelySingleJokerStream CreateJokerStream(string source, string eternalPerishableSource, string rentalSource, int ante, MotelyJokerStreamFlags flags, bool isCached, bool includeResampleStream = false)
    {
        return new()
        {
            StreamSuffix = source + ante,
            RarityPrngStream = CreatePrngStream(MotelyPrngKeys.JokerRarity + ante + source, isCached),
            EditionPrngStream = !flags.HasFlag(MotelyJokerStreamFlags.ExcludeEdition) ?
                CreatePrngStream(MotelyPrngKeys.JokerEdition + source + ante, isCached) : MotelySinglePrngStream.Invalid,
            EternalPerishablePrngStream = (!flags.HasFlag(MotelyJokerStreamFlags.ExcludeStickers) && Stake >= MotelyStake.Black) ?
                CreatePrngStream(eternalPerishableSource + ante, isCached) : MotelySinglePrngStream.Invalid,
            RentalPrngStream = (!flags.HasFlag(MotelyJokerStreamFlags.ExcludeStickers) && Stake >= MotelyStake.Gold) ?
                CreatePrngStream(rentalSource + ante, isCached) : MotelySinglePrngStream.Invalid,
            ResampleKey = includeResampleStream ?
                source + ante : null,
            CommonJokerPrngStream = new(flags.HasFlag(MotelyJokerStreamFlags.ExcludeCommonJokers) ? -2 : -1),
            UncommonJokerPrngStream = new(flags.HasFlag(MotelyJokerStreamFlags.ExcludeUncommonJokers) ? -2 : -1),
            RareJokerPrngStream = new(flags.HasFlag(MotelyJokerStreamFlags.ExcludeRareJokers) ? -2 : -1),
        };
    }

    public MotelySingleJokerFixedRarityStream CreateSoulJokerStream(int ante, MotelyJokerStreamFlags flags = MotelyJokerStreamFlags.Default, bool isCached = false)
    {
    var stream = CreateJokerFixedRarityStream(
            MotelyPrngKeys.JokerSoulSource,
            MotelyPrngKeys.ShopJokerEternalPerishableSource,
            MotelyPrngKeys.ShopJokerRentalSource,
            ante, flags, MotelyJokerRarity.Legendary, isCached
        );
    Debug.Assert(stream.DoesProvideEdition, "Soul joker stream should provide editions unless explicitly excluded");
    return stream;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelySingleJokerFixedRarityStream CreateJokerFixedRarityStream(string source, string eternalPerishableSource, string rentalSource, int ante, MotelyJokerStreamFlags flags, MotelyJokerRarity rarity, bool isCached)
    {
        return new()
        {
            Rarity = rarity,
            JokerPrngStream = CreatePrngStream(MotelyPrngKeys.FixedRarityJoker(rarity, source, ante), isCached),
            EditionPrngStream = !flags.HasFlag(MotelyJokerStreamFlags.ExcludeEdition) ?
                CreatePrngStream(MotelyPrngKeys.JokerEdition + source + ante, isCached) : MotelySinglePrngStream.Invalid,
            EternalPerishablePrngStream = (!flags.HasFlag(MotelyJokerStreamFlags.ExcludeStickers) && Stake >= MotelyStake.Black) ?
                CreatePrngStream(eternalPerishableSource + ante, isCached) : MotelySinglePrngStream.Invalid,
            RentalPrngStream = (!flags.HasFlag(MotelyJokerStreamFlags.ExcludeStickers) && Stake >= MotelyStake.Gold) ?
                CreatePrngStream(rentalSource + ante, isCached) : MotelySinglePrngStream.Invalid,
        };
    }

    public MotelySingleItemSet GetNextBuffoonPackContents(ref MotelySingleJokerStream jokerStream, MotelyBoosterPackSize size)
    => GetNextBuffoonPackContents(ref jokerStream, MotelyBoosterPackType.Buffoon.GetCardCount(size));

    public MotelySingleItemSet GetNextBuffoonPackContents(ref MotelySingleJokerStream jokerStream, int size)
    {
        Debug.Assert(size <= MotelySingleItemSet.MaxLength);

        MotelySingleItemSet pack = new();

        for (int i = 0; i < size; i++)
            pack.Append(GetNextJoker(ref jokerStream, pack)); // Handle duplicates

        return pack;
    }


#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelyItemEdition GetNextEdition(ref MotelySinglePrngStream stream, int editionRate)
    {
        double editionPoll = GetNextRandom(ref stream);

        if (editionPoll > 0.997)
            return MotelyItemEdition.Negative;
        else if (editionPoll > 1 - 0.006 * editionRate)
            return MotelyItemEdition.Polychrome;
        else if (editionPoll > 1 - 0.02 * editionRate)
            return MotelyItemEdition.Holographic;
        else if (editionPoll > 1 - 0.04 * editionRate)
            return MotelyItemEdition.Foil;
        else
            return MotelyItemEdition.None;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelyItem ApplyNextStickers(MotelyItem item, ref MotelySinglePrngStream eternalPerishableStream, ref MotelySinglePrngStream rentalStream)
    {
        if (Stake < MotelyStake.Black) return item;

        Debug.Assert(!eternalPerishableStream.IsInvalid);

        double stickerPoll = GetNextRandom(ref eternalPerishableStream);

        item = item.WithEternal(stickerPoll > 0.7);

        if (Stake < MotelyStake.Orange) return item;

        item = item.WithPerishable(stickerPoll > 0.4 && stickerPoll <= 0.7);

        if (Stake < MotelyStake.Gold) return item;

        Debug.Assert(!rentalStream.IsInvalid);

        stickerPoll = GetNextRandom(ref rentalStream);

        item = item.WithRental(stickerPoll > 0.7);

        return item;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyItem GetNextJoker(ref MotelySingleJokerFixedRarityStream stream)
    {

        MotelyItem item;

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
    // Overload that handles duplicate checking for packs using resample stream
    public MotelyItem GetNextJoker(ref MotelySingleJokerStream stream, in MotelySingleItemSet itemSet)
    {
        Debug.Assert(stream.ResampleKey != null, "Joker stream should have resample key for duplicate handling in packs");
        
        MotelyItem joker = GetNextJoker(ref stream);
        
        // If we got an excluded joker, don't check for duplicates
        if (joker.Type == MotelyItemType.JokerExcludedByStream)
            return joker;
        
        // If no duplicate, return immediately
        if (!itemSet.Contains(joker))
            return joker;
        
        // We have a duplicate - need to use resample streams
        // Create the resample stream only when we actually need it
        var resampleStream = CreateResampleStream(stream.ResampleKey, false);
        int resampleCount = 0;  // Motely uses +2 offset, so 0 becomes _resample2
        
        // Keep rerolling while we have duplicates, using resample streams
        // This matches the Balatro behavior where duplicate jokers in packs are rerolled
        while (itemSet.Contains(joker))
        {
            // Use resample stream for rerolls to avoid affecting main PRNG state
            joker = GetNextJokerFromResampleStream(ref stream, ref resampleStream, resampleCount, joker);
            resampleCount++;
            
            if (joker.Type == MotelyItemType.JokerExcludedByStream)
                return joker;
        }
        
        return joker;
    }
    
    // Helper method to get joker from resample stream
    // We need to pass in the original joker to know which rarity to resample
    private MotelyItem GetNextJokerFromResampleStream(ref MotelySingleJokerStream stream, ref MotelySingleResampleStream resampleStream, int resampleIndex, MotelyItem originalJoker)
    {
        MotelyJoker joker;
        
        // Determine the rarity of the original joker to resample within the same rarity tier
        MotelyJokerRarity originalRarity = (MotelyJokerRarity)((int)originalJoker.Type & 0xF00);
        
        if (originalRarity == MotelyJokerRarity.Rare)
        {
            // Build the resample key for rare jokers: "Joker3" + stream suffix (e.g., "buf1")
            // This matches Balatro's pool key format
            string rarityResampleKey = MotelyPrngKeys.JokerRare + stream.StreamSuffix;
            ref var resamplePrngStream = ref GetResamplePrngStream(ref resampleStream, rarityResampleKey, resampleIndex);
            
            joker = GetNextJoker<MotelyJokerRare>(ref resamplePrngStream, MotelyJokerRarity.Rare);
        }
        else if (originalRarity == MotelyJokerRarity.Uncommon)
        {
            // Build the resample key for uncommon jokers: "Joker2" + stream suffix
            string rarityResampleKey = MotelyPrngKeys.JokerUncommon + stream.StreamSuffix;
            ref var resamplePrngStream = ref GetResamplePrngStream(ref resampleStream, rarityResampleKey, resampleIndex);
            
            joker = GetNextJoker<MotelyJokerUncommon>(ref resamplePrngStream, MotelyJokerRarity.Uncommon);
        }
        else
        {
            // Build the resample key for common jokers: "Joker1" + stream suffix
            string rarityResampleKey = MotelyPrngKeys.JokerCommon + stream.StreamSuffix;
            ref var resamplePrngStream = ref GetResamplePrngStream(ref resampleStream, rarityResampleKey, resampleIndex);
            
            joker = GetNextJoker<MotelyJokerCommon>(ref resamplePrngStream, MotelyJokerRarity.Common);
        }
        
        MotelyItem jokerItem = new(joker);
        
        // Copy the edition and stickers from the original joker
        // (Balatro doesn't re-roll these properties, just the joker itself)
        jokerItem = jokerItem.WithEdition(originalJoker.Edition);
        if (originalJoker.IsEternal) jokerItem = jokerItem.WithEternal(true);
        if (originalJoker.IsPerishable) jokerItem = jokerItem.WithPerishable(true);
        if (originalJoker.IsRental) jokerItem = jokerItem.WithRental(true);
        
        return jokerItem;
    }
    
    // Helper for applying stickers from resample stream
    private MotelyItem ApplyNextStickersFromResample(MotelyItem item, ref MotelySinglePrngStream resampleStream)
    {
        if (Stake < MotelyStake.Black) return item;
        
        double stickerPoll = GetNextRandom(ref resampleStream);
        
        item = item.WithEternal(stickerPoll > 0.7);
        
        if (Stake < MotelyStake.Orange) return item;
        
        item = item.WithPerishable(stickerPoll > 0.4 && stickerPoll <= 0.7);
        
        if (Stake < MotelyStake.Gold) return item;
        
        // Use another roll for rental
        stickerPoll = GetNextRandom(ref resampleStream);
        
        item = item.WithRental(stickerPoll > 0.7);
        
        return item;
    }
    
    public MotelyItem GetNextJoker(ref MotelySingleJokerStream stream)
    {
        MotelyJoker joker;

        double rarityPoll = GetNextRandom(ref stream.RarityPrngStream);

        if (rarityPoll > 0.95)
        {
            if (!stream.DoesProvideRareJokers)
                return new(MotelyItemType.JokerExcludedByStream);

            if (stream.RareJokerPrngStream.IsInvalid)
                stream.RareJokerPrngStream = CreatePrngStream(MotelyPrngKeys.JokerRare + stream.StreamSuffix);

            joker = GetNextJoker<MotelyJokerRare>(ref stream.RareJokerPrngStream, MotelyJokerRarity.Rare);
        }
        else if (rarityPoll > 0.7)
        {
            if (!stream.DoesProvideUncommonJokers)
                return new(MotelyItemType.JokerExcludedByStream);

            if (stream.UncommonJokerPrngStream.IsInvalid)
                stream.UncommonJokerPrngStream = CreatePrngStream(MotelyPrngKeys.JokerUncommon + stream.StreamSuffix);

            joker = GetNextJoker<MotelyJokerUncommon>(ref stream.UncommonJokerPrngStream, MotelyJokerRarity.Uncommon);
        }
        else
        {
            if (!stream.DoesProvideCommonJokers)
                return new(MotelyItemType.JokerExcludedByStream);

            if (stream.CommonJokerPrngStream.IsInvalid)
                stream.CommonJokerPrngStream = CreatePrngStream(MotelyPrngKeys.JokerCommon + stream.StreamSuffix);

            joker = GetNextJoker<MotelyJokerCommon>(ref stream.CommonJokerPrngStream, MotelyJokerRarity.Common);
        }

        MotelyItem jokerItem = new(joker);

        if (stream.DoesProvideEdition)
        {
            jokerItem = jokerItem.WithEdition(GetNextEdition(ref stream.EditionPrngStream, 1));
        }

        if (stream.DoesProvideStickers)
        {
            jokerItem = ApplyNextStickers(jokerItem, ref stream.EternalPerishablePrngStream, ref stream.RentalPrngStream);
        }

        return jokerItem;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelyJoker GetNextJoker<T>(ref MotelySinglePrngStream stream, MotelyJokerRarity rarity) where T : unmanaged, Enum
    {
        Debug.Assert(sizeof(T) == 4);
        int value = (int)rarity | GetNextRandomInt(ref stream, 0, MotelyEnum<T>.ValueCount);
        return (MotelyJoker)value;
    }
}