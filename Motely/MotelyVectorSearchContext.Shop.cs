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
    public MotelyItemVector GetNextShopItem(ref MotelyVectorShopItemStream stream)
    {
        Vector512<double> itemTypePoll = GetNextRandom(ref stream.ItemTypeStream) * stream.TotalRate;
        Vector512<double> shopJokerRateVector = Vector512.Create((double)ShopJokerRate);
        
        // Check if it's a joker
        Vector512<double> isJoker = Vector512.LessThan(itemTypePoll, shopJokerRateVector);
        MotelyItemVector jokerResult = stream.DoesProvideJokers ? 
            GetNextJoker(ref stream.JokerStream) : 
            new MotelyItemVector(new MotelyItem(MotelyItemType.JokerExcludedByStream));
            
        itemTypePoll -= shopJokerRateVector;
        
        // Check if it's a tarot
        Vector512<double> isTarot = Vector512.LessThan(itemTypePoll, stream.TarotRate);
        MotelyItemVector tarotResult = stream.DoesProvideTarots ?
            GetNextTarot(ref stream.TarotStream) :
            new MotelyItemVector(new MotelyItem(MotelyItemType.TarotExcludedByStream));
            
        itemTypePoll -= stream.TarotRate;
        
        // Check if it's a planet
        Vector512<double> isPlanet = Vector512.LessThan(itemTypePoll, stream.PlanetRate);
        MotelyItemVector planetResult = stream.DoesProvidePlanets ?
            GetNextPlanet(ref stream.PlanetStream) :
            new MotelyItemVector(new MotelyItem(MotelyItemType.PlanetExcludedByStream));
            
        itemTypePoll -= stream.PlanetRate;
        
        // Check if it's a playing card
        Vector512<double> isPlayingCard = Vector512.LessThan(itemTypePoll, stream.PlayingCardRate);
        MotelyItemVector playingCardResult = new MotelyItemVector(new MotelyItem(MotelyItemType.NotImplemented));
        
        // Otherwise it's a spectral
        MotelyItemVector spectralResult = stream.DoesProvideSpectrals ?
            GetNextSpectral(ref stream.SpectralStream) :
            new MotelyItemVector(new MotelyItem(MotelyItemType.SpectralExcludedByStream));
            
        // Select the appropriate result based on item type
        MotelyItemVector result = new MotelyItemVector(Vector256.ConditionalSelect(
            MotelyVectorUtils.ShrinkDoubleMaskToInt(isJoker), jokerResult.Value,
            Vector256.ConditionalSelect(
                MotelyVectorUtils.ShrinkDoubleMaskToInt(isTarot), tarotResult.Value,
                Vector256.ConditionalSelect(
                    MotelyVectorUtils.ShrinkDoubleMaskToInt(isPlanet), planetResult.Value,
                    Vector256.ConditionalSelect(
                        MotelyVectorUtils.ShrinkDoubleMaskToInt(isPlayingCard), playingCardResult.Value, spectralResult.Value)))));
                     
        return result;
    }
}