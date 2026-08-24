using System.Runtime.CompilerServices;

namespace Motely;

public struct MotelySingleShopItemStream
{
    public double TarotRate;
    public double PlanetRate;
    public double StandardcardRate;
    public double SpectralRate;
    public double TotalRate;
    public MotelySinglePrngStream ItemTypeStream;
    public MotelySingleJokerStream JokerStream;
    public MotelySingleTarotStream TarotStream;
    public MotelySinglePlanetStream PlanetStream;
    public MotelySingleSpectralStream SpectralStream;
    public MotelySinglePrngStream StandardCardStream;

    public readonly bool DoesProvideJokers => !JokerStream.IsNull;
    public readonly bool DoesProvideTarots => !TarotStream.IsNull;
    public readonly bool DoesProvidePlanets => !PlanetStream.IsNull;
    public readonly bool DoesProvideSpectrals => !SpectralStream.IsNull;
    public readonly bool DoesProvideStandardCards => !StandardCardStream.IsInvalid;
}

[Flags]
public enum MotelyShopStreamFlags
{
    ExcludeJokers = 1 << 1,
    ExcludeTarots = 1 << 2,
    ExcludePlanets = 1 << 3,
    ExcludeSpectrals = 1 << 4,
    ExcludeStandardCards = 1 << 5,

    Default = 0,
}

public partial class MotelySingleSearchContext
{
    // Internal rather than private so the rarity model reads the same constant the shop rolls
    // against, instead of carrying its own copy that can drift.
    internal const int ShopJokerRate = 20;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelySingleShopItemStream CreateShopItemStream(
        int ante,
        MotelyShopStreamFlags flags = MotelyShopStreamFlags.Default,
        MotelyJokerStreamFlags jokerFlags = MotelyJokerStreamFlags.Default,
        bool isCached = false
    )
    {
        return CreateShopItemStream(ante, Deck.GetDefaultRunState(), flags, jokerFlags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelySingleShopItemStream CreateShopItemStream(
        int ante,
        MotelyRunState runState,
        MotelyShopStreamFlags flags = MotelyShopStreamFlags.Default,
        MotelyJokerStreamFlags jokerFlags = MotelyJokerStreamFlags.Default,
        bool isCached = false
    )
    {
        MotelySingleShopItemStream stream = new()
        {
            ItemTypeStream = CreatePrngStream(MotelyPrngKeys.ShopItemType + ante, isCached),
            JokerStream = flags.HasFlag(MotelyShopStreamFlags.ExcludeJokers)
                ? default
                : CreateShopJokerStream(ante, jokerFlags, isCached),
            TarotStream = flags.HasFlag(MotelyShopStreamFlags.ExcludeTarots)
                ? default
                : CreateShopTarotStream(ante, isCached),
            PlanetStream = flags.HasFlag(MotelyShopStreamFlags.ExcludePlanets)
                ? default
                : CreateShopPlanetStream(ante, isCached),
            SpectralStream =
                flags.HasFlag(MotelyShopStreamFlags.ExcludeSpectrals) || Deck != MotelyDeck.Ghost
                    ? default
                    : CreateShopSpectralStream(ante, isCached),
            // Deliberately uncached: CacheShopStream never registers this key, and the pseudohash
            // cache is keyed by key *length*. "front"+"sho"+ante is 9 chars — the same length as
            // the shop tarot key ("Tarot"+"sho"+ante) — so isCached:true only ever resolved
            // because tarots happened to register 9 first. Under ExcludeTarots nothing registers
            // it and GetPartialHashVector dereferences a null cache slot.
            StandardCardStream = flags.HasFlag(MotelyShopStreamFlags.ExcludeStandardCards)
                ? MotelySinglePrngStream.Invalid
                : CreatePrngStream(
                    MotelyPrngKeys.StandardCardBase + MotelyPrngKeys.ShopItemSource + ante
                ),

            TarotRate = 4,
            PlanetRate = 4,
            StandardcardRate = 0,
            SpectralRate = 0,
        };

        if (Deck == MotelyDeck.Ghost)
        {
            stream.SpectralRate = 2;
        }

        if (runState.IsVoucherActive(MotelyVoucher.TarotTycoon))
        {
            stream.TarotRate = 32;
        }
        else if (runState.IsVoucherActive(MotelyVoucher.TarotMerchant))
        {
            stream.TarotRate = 9.6;
        }

        if (runState.IsVoucherActive(MotelyVoucher.PlanetTycoon))
        {
            stream.PlanetRate = 32;
        }
        else if (runState.IsVoucherActive(MotelyVoucher.PlanetMerchant))
        {
            stream.PlanetRate = 9.6;
        }

        if (runState.IsVoucherActive(MotelyVoucher.MagicTrick))
        {
            stream.StandardcardRate = 4;
        }

        stream.TotalRate =
            ShopJokerRate
            + stream.TarotRate
            + stream.PlanetRate
            + stream.StandardcardRate
            + stream.SpectralRate;

        return stream;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem GetNextShopItem(ref MotelySingleShopItemStream stream)
    {
        double itemTypePoll = GetNextRandom(ref stream.ItemTypeStream) * stream.TotalRate;

        if (itemTypePoll < ShopJokerRate)
        {
            if (!stream.DoesProvideJokers)
                return new(MotelyItemType.JokerExcludedByStream);

            return GetNextJoker(ref stream.JokerStream);
        }

        itemTypePoll -= ShopJokerRate;

        if (itemTypePoll < stream.TarotRate)
        {
            if (!stream.DoesProvideTarots)
                return new(MotelyItemType.TarotExcludedByStream);

            return GetNextTarot(ref stream.TarotStream);
        }

        itemTypePoll -= stream.TarotRate;

        if (itemTypePoll < stream.PlanetRate)
        {
            if (!stream.DoesProvidePlanets)
                return new(MotelyItemType.PlanetExcludedByStream);

            return GetNextPlanet(ref stream.PlanetStream);
        }

        itemTypePoll -= stream.PlanetRate;

        if (itemTypePoll < stream.StandardcardRate)
        {
            if (!stream.DoesProvideStandardCards)
                return new(MotelyItemType.StandardCardExcludedByStream);

            // Magic Trick shop card = Balatro's create_card('Base', ..., 'sho'): a bare playing
            // card, one 'front'+'sho'+ante pull. create_card only applies enhancement/edition/seal
            // inside its `_type=='Joker'` block, which a 'Base' card never enters, so none apply.
            // (The Illusion voucher's edition/enhancement layer is not mirrored yet.)
            return GetNextShopStandardCard(ref stream.StandardCardStream);
        }

        // This shop will generate a Spectral card
        if (!stream.DoesProvideSpectrals)
            return new(MotelyItemType.SpectralExcludedByStream);

        return GetNextSpectral(ref stream.SpectralStream);
    }

    /// <summary>
    /// The bare playing card a shop slot yields when Magic Trick is active. Mirrors Balatro's
    /// create_card('Base', ..., 'sho'): a single 'front'+'sho'+ante draw for rank+suit, no
    /// enhancement/edition/seal. Sequential-only by design — no SIMD prefilter queries shop
    /// standard cards, so the vector path leaves them unread.
    /// </summary>
    public MotelyItem GetNextShopStandardCard(ref MotelySinglePrngStream cardStream)
        => new(
            MotelyEnum<MotelyStandardCard>.Values[
                GetNextRandomInt(ref cardStream, 0, MotelyEnum<MotelyStandardCard>.ValueCount)
            ]
        );
}
