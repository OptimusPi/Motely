using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

// Self-contained, position-aware shop joker stream
public ref struct MotelyVectorShopJokerStream
{
    // Private position tracking - each stream has its OWN copy!
    public MotelyVectorPrngStream ItemTypeStream;
    
    // Joker generation streams
    public MotelyVectorJokerStream JokerStream;
    
    // Shop rates
    public Vector512<double> TotalRate;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector GetNext(ref MotelyVectorSearchContext ctx)
    {
        // Determine slot type internally - no external coordination!
        var itemTypePoll = ctx.GetNextRandom(ref ItemTypeStream) * TotalRate;
        var isJokerSlot = Vector512.LessThan(itemTypePoll, Vector512.Create(20.0));
        
        // Generate joker only for joker slots (masked)
        var joker = ctx.GetNextJoker(ref JokerStream, isJokerSlot);
        
        // Return joker or JokerExcludedByStream
        var jokerIntMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(isJokerSlot);
        var excluded = Vector256.Create((int)MotelyItemType.JokerExcludedByStream);
        
        return new MotelyItemVector(
            Vector256.ConditionalSelect(jokerIntMask, joker.Value, excluded)
        );
    }
}

// Self-contained, position-aware shop tarot stream
public ref struct MotelyVectorShopTarotStream
{
    // Public position tracking
    public MotelyVectorPrngStream ItemTypeStream;
    
    // Tarot generation stream
    public MotelyVectorTarotStream TarotStream;
    
    // Shop rates
    public Vector512<double> TarotRate;
    public Vector512<double> TotalRate;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector GetNext(ref MotelyVectorSearchContext ctx)
    {
        // Determine slot type internally
        var itemTypePoll = ctx.GetNextRandom(ref ItemTypeStream) * TotalRate;
        itemTypePoll -= Vector512.Create(20.0); // Skip joker range
        var isTarotSlot = Vector512.LessThan(itemTypePoll, TarotRate);
        
        // Generate tarot only for tarot slots (masked)
        var tarot = ctx.GetNextTarot(ref TarotStream, isTarotSlot);
        
        // Return tarot or TarotExcludedByStream
        var tarotIntMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(isTarotSlot);
        var excluded = Vector256.Create((int)MotelyItemType.TarotExcludedByStream);
        
        return new MotelyItemVector(
            Vector256.ConditionalSelect(tarotIntMask, tarot.Value, excluded)
        );
    }
}

// Self-contained, position-aware shop planet stream
public ref struct MotelyVectorShopPlanetStream
{
    // Public position tracking
    public MotelyVectorPrngStream ItemTypeStream;
    
    // Planet generation stream
    public MotelyVectorPlanetStream PlanetStream;
    
    // Shop rates
    public Vector512<double> PlanetRate;
    public Vector512<double> TarotRate;
    public Vector512<double> TotalRate;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector GetNext(ref MotelyVectorSearchContext ctx)
    {
        // Determine slot type internally
        var itemTypePoll = ctx.GetNextRandom(ref ItemTypeStream) * TotalRate;
        itemTypePoll -= Vector512.Create(20.0); // Skip joker range
        itemTypePoll -= TarotRate; // Skip tarot range
        var isPlanetSlot = Vector512.LessThan(itemTypePoll, PlanetRate);
        
        // Generate planet only for planet slots (masked)
        var planet = ctx.GetNextPlanet(ref PlanetStream, isPlanetSlot);
        
        // Return planet or PlanetExcludedByStream
        var planetIntMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(isPlanetSlot);
        var excluded = Vector256.Create((int)MotelyItemType.PlanetExcludedByStream);
        
        return new MotelyItemVector(
            Vector256.ConditionalSelect(planetIntMask, planet.Value, excluded)
        );
    }
}

// Self-contained, position-aware shop spectral stream
public ref struct MotelyVectorShopSpectralStream
{
    // Public position tracking
    public MotelyVectorPrngStream ItemTypeStream;
    
    // Spectral generation stream
    public MotelyVectorSpectralStream SpectralStream;
    
    // Shop rates - need ALL rates to properly skip ranges!
    public Vector512<double> TarotRate;
    public Vector512<double> PlanetRate;
    public Vector512<double> PlayingCardRate;
    public Vector512<double> SpectralRate;
    public Vector512<double> TotalRate;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector GetNext(ref MotelyVectorSearchContext ctx)
    {
        // Determine slot type internally
        var itemTypePoll = ctx.GetNextRandom(ref ItemTypeStream) * TotalRate;
        itemTypePoll -= Vector512.Create(20.0); // Skip joker range
        itemTypePoll -= TarotRate; // Skip tarot range (actual rate, not base!)
        itemTypePoll -= PlanetRate; // Skip planet range (actual rate, not base!)
        itemTypePoll -= PlayingCardRate; // Skip playing card range
        var isSpectralSlot = Vector512.LessThan(itemTypePoll, SpectralRate);
        
        // Generate spectral only for spectral slots (masked)
        var spectral = ctx.GetNextSpectral(ref SpectralStream, isSpectralSlot);
        
        // Return spectral or SpectralExcludedByStream
        var spectralIntMask = MotelyVectorUtils.ShrinkDoubleMaskToInt(isSpectralSlot);
        var excluded = Vector256.Create((int)MotelyItemType.SpectralExcludedByStream);
        
        return new MotelyItemVector(
            Vector256.ConditionalSelect(spectralIntMask, spectral.Value, excluded)
        );
    }
}

ref partial struct MotelyVectorSearchContext
{
    // Create the new self-contained shop streams
    public MotelyVectorShopJokerStream CreateShopJokerSlotStream(int ante, 
        MotelyJokerStreamFlags jokerFlags = MotelyJokerStreamFlags.Default, bool isCached = false)
    {
        var runState = Deck.GetDefaultRunState();
        var stream = new MotelyVectorShopJokerStream();
        stream.ItemTypeStream = CreatePrngStream(MotelyPrngKeys.ShopItemType + ante, isCached);
        stream.JokerStream = CreateShopJokerStream(ante, jokerFlags, isCached);
        
        // Calculate total rate including voucher effects
        var tarotRate = Vector512.Create(4.0);
        var planetRate = Vector512.Create(4.0);
        var playingCardRate = Vector512.Create(0.0);
        var spectralRate = Deck == MotelyDeck.Ghost ? Vector512.Create(2.0) : Vector512.Create(0.0);
        
        if (runState.IsVoucherActive(MotelyVoucher.TarotTycoon))
            tarotRate = Vector512.Create(32.0);
        else if (runState.IsVoucherActive(MotelyVoucher.TarotMerchant))
            tarotRate = Vector512.Create(9.6);
        
        if (runState.IsVoucherActive(MotelyVoucher.PlanetTycoon))
            planetRate = Vector512.Create(32.0);
        else if (runState.IsVoucherActive(MotelyVoucher.PlanetMerchant))
            planetRate = Vector512.Create(9.6);
        
        if (runState.IsVoucherActive(MotelyVoucher.MagicTrick))
            playingCardRate = Vector512.Create(4.0);
        
        stream.TotalRate = Vector512.Create(20.0) + tarotRate + planetRate + playingCardRate + spectralRate;
        return stream;
    }
    
    public MotelyVectorShopTarotStream CreateShopTarotSlotStream(int ante, bool isCached = false)
    {
        var runState = Deck.GetDefaultRunState();
        var stream = new MotelyVectorShopTarotStream();
        stream.ItemTypeStream = CreatePrngStream(MotelyPrngKeys.ShopItemType + ante, isCached);
        stream.TarotStream = CreateShopTarotStream(ante, isCached);
        
        // Calculate rates
        stream.TarotRate = Vector512.Create(4.0);
        var planetRate = Vector512.Create(4.0);
        var playingCardRate = Vector512.Create(0.0);
        var spectralRate = Deck == MotelyDeck.Ghost ? Vector512.Create(2.0) : Vector512.Create(0.0);
        
        if (runState.IsVoucherActive(MotelyVoucher.TarotTycoon))
            stream.TarotRate = Vector512.Create(32.0);
        else if (runState.IsVoucherActive(MotelyVoucher.TarotMerchant))
            stream.TarotRate = Vector512.Create(9.6);
        
        if (runState.IsVoucherActive(MotelyVoucher.PlanetTycoon))
            planetRate = Vector512.Create(32.0);
        else if (runState.IsVoucherActive(MotelyVoucher.PlanetMerchant))
            planetRate = Vector512.Create(9.6);
        
        if (runState.IsVoucherActive(MotelyVoucher.MagicTrick))
            playingCardRate = Vector512.Create(4.0);
        
        stream.TotalRate = Vector512.Create(20.0) + stream.TarotRate + planetRate + playingCardRate + spectralRate;
        return stream;
    }
    
    public MotelyVectorShopPlanetStream CreateShopPlanetSlotStream(int ante, bool isCached = false)
    {
        var runState = Deck.GetDefaultRunState();
        var stream = new MotelyVectorShopPlanetStream();
        stream.ItemTypeStream = CreatePrngStream(MotelyPrngKeys.ShopItemType + ante, isCached);
        stream.PlanetStream = CreateShopPlanetStream(ante, isCached);
        
        // Calculate rates
        stream.TarotRate = Vector512.Create(4.0);
        stream.PlanetRate = Vector512.Create(4.0);
        var playingCardRate = Vector512.Create(0.0);
        var spectralRate = Deck == MotelyDeck.Ghost ? Vector512.Create(2.0) : Vector512.Create(0.0);
        
        if (runState.IsVoucherActive(MotelyVoucher.TarotTycoon))
            stream.TarotRate = Vector512.Create(32.0);
        else if (runState.IsVoucherActive(MotelyVoucher.TarotMerchant))
            stream.TarotRate = Vector512.Create(9.6);
        
        if (runState.IsVoucherActive(MotelyVoucher.PlanetTycoon))
            stream.PlanetRate = Vector512.Create(32.0);
        else if (runState.IsVoucherActive(MotelyVoucher.PlanetMerchant))
            stream.PlanetRate = Vector512.Create(9.6);
        
        if (runState.IsVoucherActive(MotelyVoucher.MagicTrick))
            playingCardRate = Vector512.Create(4.0);
        
        stream.TotalRate = Vector512.Create(20.0) + stream.TarotRate + stream.PlanetRate + playingCardRate + spectralRate;
        return stream;
    }
    
    public MotelyVectorShopSpectralStream CreateShopSpectralSlotStream(int ante, bool isCached = false)
    {
        var runState = Deck.GetDefaultRunState();
        var stream = new MotelyVectorShopSpectralStream();
        stream.ItemTypeStream = CreatePrngStream(MotelyPrngKeys.ShopItemType + ante, isCached);
        stream.SpectralStream = CreateShopSpectralStream(ante, isCached);
        
        // Calculate rates - store them ALL in the stream!
        stream.TarotRate = Vector512.Create(4.0);
        stream.PlanetRate = Vector512.Create(4.0);
        stream.PlayingCardRate = Vector512.Create(0.0);
        stream.SpectralRate = Deck == MotelyDeck.Ghost ? Vector512.Create(2.0) : Vector512.Create(0.0);
        
        if (runState.IsVoucherActive(MotelyVoucher.TarotTycoon))
            stream.TarotRate = Vector512.Create(32.0);
        else if (runState.IsVoucherActive(MotelyVoucher.TarotMerchant))
            stream.TarotRate = Vector512.Create(9.6);
        
        if (runState.IsVoucherActive(MotelyVoucher.PlanetTycoon))
            stream.PlanetRate = Vector512.Create(32.0);
        else if (runState.IsVoucherActive(MotelyVoucher.PlanetMerchant))
            stream.PlanetRate = Vector512.Create(9.6);
        
        if (runState.IsVoucherActive(MotelyVoucher.MagicTrick))
            stream.PlayingCardRate = Vector512.Create(4.0);
        
        stream.TotalRate = Vector512.Create(20.0) + stream.TarotRate + stream.PlanetRate + stream.PlayingCardRate + stream.SpectralRate;
        return stream;
    }
}