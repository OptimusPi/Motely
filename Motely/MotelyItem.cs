using System.Runtime.CompilerServices;

namespace Motely;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct MotelyItem(int value) : IEquatable<MotelyItem>
{
    public readonly int Value = value;

    public readonly MotelyItemType Type => (MotelyItemType)(Value & MotelyGlobals.ItemTypeMask);
    public readonly MotelyItemTypeCategory TypeCategory =>
        (MotelyItemTypeCategory)(Value & MotelyGlobals.ItemTypeCategoryMask);
    public readonly MotelyItemSeal Seal => (MotelyItemSeal)(Value & MotelyGlobals.ItemSealMask);
    public readonly MotelyItemEnhancement Enhancement =>
        (MotelyItemEnhancement)(Value & MotelyGlobals.ItemEnhancementMask);
    public readonly MotelyItemEdition Edition =>
        (MotelyItemEdition)(Value & MotelyGlobals.ItemEditionMask);

    public readonly MotelyPlayingCardSuit PlayingCardSuit =>
        (MotelyPlayingCardSuit)(Value & MotelyGlobals.PlayingCardSuitMask);
    public readonly MotelyPlayingCardRank PlayingCardRank =>
        (MotelyPlayingCardRank)(Value & MotelyGlobals.PlayingCardRankMask);

    public readonly bool IsPerishable => (Value & (1 << MotelyGlobals.PerishableStickerOffset)) != 0;
    public readonly bool IsEternal => (Value & (1 << MotelyGlobals.EternalStickerOffset)) != 0;
    public readonly bool IsRental => (Value & (1 << MotelyGlobals.RentalStickerOffset)) != 0;

    public readonly bool IsInvalid => TypeCategory == MotelyItemTypeCategory.Invalid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem(MotelyItemType type)
        : this((int)type) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem(MotelyPlayingCard card)
        : this((int)card | (int)MotelyItemTypeCategory.PlayingCard) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem(MotelyJoker joker, MotelyItemEdition edition = MotelyItemEdition.None)
        : this((int)joker | (int)MotelyItemTypeCategory.Joker | (int)edition) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem AsType(MotelyItemType type)
    {
        return new((Value & ~MotelyGlobals.ItemTypeMask) | (int)type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithSeal(MotelyItemSeal seal)
    {
        return new((Value & ~MotelyGlobals.ItemSealMask) | (int)seal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEnhancement(MotelyItemEnhancement enhancement)
    {
        return new((Value & ~MotelyGlobals.ItemEnhancementMask) | (int)enhancement);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEdition(MotelyItemEdition edition)
    {
        return new((Value & ~MotelyGlobals.ItemEditionMask) | (int)edition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithPerishable(bool isPerishable)
    {
        int mask = 1 << MotelyGlobals.PerishableStickerOffset;
        return new(isPerishable ? (Value | mask) : (Value & ~mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEternal(bool isEternal)
    {
        int mask = 1 << MotelyGlobals.EternalStickerOffset;
        return new(isEternal ? (Value | mask) : (Value & ~mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithRental(bool isRental)
    {
        int mask = 1 << MotelyGlobals.RentalStickerOffset;
        return new(isRental ? (Value | mask) : (Value & ~mask));
    }

    public override string ToString()
    {
        string stringified = Type.ToString();

        if (Enhancement != MotelyItemEnhancement.None)
        {
            stringified = Enhancement + " " + stringified;
        }

        if (Edition != MotelyItemEdition.None)
        {
            stringified = Edition + " " + stringified;
        }

        if (IsPerishable)
            stringified = "Perishable " + stringified;
        if (IsEternal)
            stringified = "Eternal " + stringified;
        if (IsRental)
            stringified = "Rental " + stringified;

        if (Seal != MotelyItemSeal.None)
        {
            stringified = Seal + " Seal " + stringified;
        }

        return stringified;
    }

    /// <summary>
    /// Parses a string produced by <see cref="FormatUtils.FormatItem"/> (a &quot;jummy&quot;).
    /// Prefix order matches <see cref="FormatUtils.FormatItem"/> (stickers, seal, edition, enhancement, type).
    /// </summary>
    /// <exception cref="FormatException">Unrecognized layout or unknown type.</exception>
    public static MotelyItem Parse(string jummy) => FormatUtils.ParseMotelyItem(jummy);

    /// <inheritdoc cref="Parse"/>
    public static bool TryParse(string jummy, out MotelyItem item) =>
        FormatUtils.TryParseMotelyItem(jummy, out item);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MotelyItem other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is MotelyItem item && Equals(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(MotelyItem a, MotelyItem b) => a.Equals(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(MotelyItem a, MotelyItem b) => !a.Equals(b);

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static implicit operator MotelyItem(MotelyItemType type) => new(type);
}
