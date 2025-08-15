
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Motely;

public ref struct MotelySingleSpectralStream(string resampleKey, MotelySingleResampleStream resampleStream, MotelySinglePrngStream blackHoleStream)
{
    public readonly bool IsNull => ResampleKey == null;
    public readonly string ResampleKey = resampleKey;
    public MotelySingleResampleStream ResampleStream = resampleStream;
    public MotelySinglePrngStream SoulBlackHolePrngStream = blackHoleStream;
    public readonly bool IsSoulBlackHoleable => !SoulBlackHolePrngStream.IsInvalid;
}

ref partial struct MotelySingleSearchContext
{
#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private MotelySingleSpectralStream CreateSpectralStream(string source, int ante, bool searchSpectral, bool soulBlackHoleable, bool isCached)
    {
        return new(
            MotelyPrngKeys.Spectral + source + ante,
            searchSpectral ?
                CreateResampleStream(MotelyPrngKeys.Spectral + source + ante, isCached) :
                MotelySingleResampleStream.Invalid,
            soulBlackHoleable ?
                CreatePrngStream(MotelyPrngKeys.SpectralSoulBlackHole + MotelyPrngKeys.Spectral + ante, isCached) :
                MotelySinglePrngStream.Invalid
        );
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleSpectralStream CreateSpectralPackSpectralStream(int ante, bool soulOnly = false, bool isCached = false) =>
        CreateSpectralStream(MotelyPrngKeys.SpectralPackItemSource, ante, !soulOnly, true, isCached);

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif

    public MotelySingleSpectralStream CreateShopSpectralStream(int ante, bool isCached = false) =>
        CreateSpectralStream(MotelyPrngKeys.ShopItemSource, ante, true, false, isCached);

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public bool GetNextSpectralPackHasTheSoul(ref MotelySingleSpectralStream spectralStream, MotelyBoosterPackSize size)
    {
        Debug.Assert(spectralStream.IsSoulBlackHoleable, "Spectral pack does not have the soul.");

        int cardCount = MotelyBoosterPackType.Spectral.GetCardCount(size);

        // Check each card position following Balatro logic:
        for (int i = 0; i < cardCount; i++)
        {
            // Soul check (0.3% chance)
            if (GetNextRandom(ref spectralStream.SoulBlackHolePrngStream) > 0.997)
            {
                return true; // Found Soul!
            }

            // BlackHole check (0.3% chance) - must still consume RNG even though we don't care about BlackHole
            GetNextRandom(ref spectralStream.SoulBlackHolePrngStream);
        }

        return false;
    }

    public MotelySingleItemSet GetNextSpectralPackContents(ref MotelySingleSpectralStream spectralStream, MotelyBoosterPackSize size)
        => GetNextSpectralPackContents(ref spectralStream, MotelyBoosterPackType.Spectral.GetCardCount(size));

    public MotelySingleItemSet GetNextSpectralPackContents(ref MotelySingleSpectralStream spectralStream, int size)
    {
        Debug.Assert(size <= MotelySingleItemSet.MaxLength);
        DebugLogger.Log($"[GetNextSpectralPackContents] Generating {size} cards");

        MotelySingleItemSet pack = new();

        for (int i = 0; i < size; i++)
        {
            var card = GetNextSpectral(ref spectralStream, pack);
            pack.Append(card);
            DebugLogger.Log($"[GetNextSpectralPackContents] Card {i}: {card.Type}");
        }

        return pack;
    }
    
    // Alias for compatibility with OuijaJsonFilterDesc
    public MotelySingleItemSet GetSpectralPackContents(ref MotelySingleSpectralStream spectralStream, MotelyBoosterPackSize size)
        => GetNextSpectralPackContents(ref spectralStream, size);
        
    public MotelySingleItemSet GetSpectralPackContents(ref MotelySingleSpectralStream spectralStream, int size)
        => GetNextSpectralPackContents(ref spectralStream, size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem GetNextSpectral(ref MotelySingleSpectralStream spectralStream)
    {
        if (spectralStream.IsSoulBlackHoleable)
        {
            // Check for Soul (0.3% chance)
            if (GetNextRandom(ref spectralStream.SoulBlackHolePrngStream) > 0.997)
            {
                return MotelyItemType.Soul;
            }

            // Check for BlackHole (0.3% chance) - cannot override Soul
            if (GetNextRandom(ref spectralStream.SoulBlackHolePrngStream) > 0.997)
            {
                return MotelyItemType.BlackHole;
            }
        }

        // Soul and BlackHole should ONLY come from special generation, not regular
        // So we limit to 0-15 (Familiar through Cryptid), excluding Soul (16) and BlackHole (17)
        return (MotelyItemType)MotelyItemTypeCategory.SpectralCard |
            (MotelyItemType)GetNextRandomInt(ref spectralStream.ResampleStream.InitialPrngStream, 0, 16);
    }

    public MotelyItem GetNextSpectral(ref MotelySingleSpectralStream SpectralStream, in MotelySingleItemSet itemSet)
    {
        if (SpectralStream.IsSoulBlackHoleable)
        {
            // CRITICAL: Only consume RNG if we're actually checking for the item!
            // Check Soul first (0.3% chance)
            if (!itemSet.Contains(MotelyItemType.Soul) && GetNextRandom(ref SpectralStream.SoulBlackHolePrngStream) > 0.997)
            {
                DebugLogger.Log($"[GetNextSpectral] FORCED SOUL!");
                return MotelyItemType.Soul;
            }

            // Check BlackHole (0.3% chance)
            if (!itemSet.Contains(MotelyItemType.BlackHole) && GetNextRandom(ref SpectralStream.SoulBlackHolePrngStream) > 0.997)
            {
                DebugLogger.Log($"[GetNextSpectral] FORCED BLACKHOLE!");
                return MotelyItemType.BlackHole;
            }
        }

        // Soul and BlackHole should ONLY come from special generation, not regular
        // So we limit to 0-15 (Familiar through Cryptid), excluding Soul (16) and BlackHole (17)
        MotelyItemType Spectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard |
            (MotelyItemType)GetNextRandomInt(ref SpectralStream.ResampleStream.InitialPrngStream, 0, 16);

        DebugLogger.Log($"[GetNextSpectral] Generated regular spectral: {Spectral}");
        int resampleCount = 0;

        while (true)
        {
            if (!itemSet.Contains(Spectral))
            {
                return Spectral;
            }

            // Resample also needs to exclude Soul/BlackHole
            Spectral = (MotelyItemType)MotelyItemTypeCategory.SpectralCard | (MotelyItemType)GetNextRandomInt(
                ref GetResamplePrngStream(ref SpectralStream.ResampleStream, SpectralStream.ResampleKey, resampleCount),
                0, 16
            );

            ++resampleCount;
        }
    }
}