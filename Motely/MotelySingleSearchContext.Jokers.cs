
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
    public string? ResampleKey; // Key to create resample stream for handling duplicates when needed

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
        Debug.Assert(!jokerStream.RarityPrngStream.IsInvalid, "Joker stream must have valid rarity PRNG");
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
        
        // Determine the rarity-specific resample key based on the original joker's rarity
        MotelyJokerRarity originalRarity = (MotelyJokerRarity)((int)joker.Type & 0xF00);
        string rarityPrefix = originalRarity switch
        {
            MotelyJokerRarity.Rare => MotelyPrngKeys.JokerRare,
            MotelyJokerRarity.Uncommon => MotelyPrngKeys.JokerUncommon,
            _ => MotelyPrngKeys.JokerCommon
        };
        string resampleKey = rarityPrefix + stream.ResampleKey;
        
        // Create the resample stream with the correct key
        var resampleStream = CreateResampleStream(resampleKey, false);
        int resampleCount = 0;
        
        // Keep rerolling while we have duplicates
        while (itemSet.Contains(joker))
        {
            ref var resamplePrngStream = ref GetResamplePrngStream(ref resampleStream, resampleKey, resampleCount);
            
            // Get new joker of same rarity
            MotelyJoker newJoker = originalRarity switch
            {
                MotelyJokerRarity.Rare => GetNextJoker<MotelyJokerRare>(ref resamplePrngStream, MotelyJokerRarity.Rare),
                MotelyJokerRarity.Uncommon => GetNextJoker<MotelyJokerUncommon>(ref resamplePrngStream, MotelyJokerRarity.Uncommon),
                _ => GetNextJoker<MotelyJokerCommon>(ref resamplePrngStream, MotelyJokerRarity.Common)
            };
            
            // Preserve edition and stickers from original (Balatro doesn't re-roll these)
            joker = new MotelyItem(newJoker)
                .WithEdition(joker.Edition);
            if (joker.IsEternal) joker = joker.WithEternal(true);
            if (joker.IsPerishable) joker = joker.WithPerishable(true);
            if (joker.IsRental) joker = joker.WithRental(true);
            
            resampleCount++;
            
            if (joker.Type == MotelyItemType.JokerExcludedByStream)
                return joker;
        }
        
        return joker;
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