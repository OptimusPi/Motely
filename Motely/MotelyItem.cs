using System.Runtime.CompilerServices;

namespace Motely;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct MotelyItem(int value) : IEquatable<MotelyItem>
{
    public readonly int Value = value;

    public readonly MotelyItemType Type => (MotelyItemType)(Value & MotelyCore.ItemTypeMask);
    public readonly MotelyItemTypeCategory TypeCategory =>
        (MotelyItemTypeCategory)(Value & MotelyCore.ItemTypeCategoryMask);
    public readonly MotelyItemSeal Seal => (MotelyItemSeal)(Value & MotelyCore.ItemSealMask);
    public readonly MotelyItemEnhancement Enhancement =>
        (MotelyItemEnhancement)(Value & MotelyCore.ItemEnhancementMask);
    public readonly MotelyItemEdition Edition =>
        (MotelyItemEdition)(Value & MotelyCore.ItemEditionMask);

    public readonly MotelyPlayingCardSuit PlayingCardSuit =>
        (MotelyPlayingCardSuit)(Value & MotelyCore.PlayingCardSuitMask);
    public readonly MotelyPlayingCardRank PlayingCardRank =>
        (MotelyPlayingCardRank)(Value & MotelyCore.PlayingCardRankMask);

    public readonly bool IsPerishable => (Value & (1 << MotelyCore.PerishableStickerOffset)) != 0;
    public readonly bool IsEternal => (Value & (1 << MotelyCore.EternalStickerOffset)) != 0;
    public readonly bool IsRental => (Value & (1 << MotelyCore.RentalStickerOffset)) != 0;

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
        return new((Value & ~MotelyCore.ItemTypeMask) | (int)type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithSeal(MotelyItemSeal seal)
    {
        return new((Value & ~MotelyCore.ItemSealMask) | (int)seal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEnhancement(MotelyItemEnhancement enhancement)
    {
        return new((Value & ~MotelyCore.ItemEnhancementMask) | (int)enhancement);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEdition(MotelyItemEdition edition)
    {
        return new((Value & ~MotelyCore.ItemEditionMask) | (int)edition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithPerishable(bool isPerishable)
    {
        int mask = 1 << MotelyCore.PerishableStickerOffset;
        return new(isPerishable ? (Value | mask) : (Value & ~mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEternal(bool isEternal)
    {
        int mask = 1 << MotelyCore.EternalStickerOffset;
        return new(isEternal ? (Value | mask) : (Value & ~mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithRental(bool isRental)
    {
        int mask = 1 << MotelyCore.RentalStickerOffset;
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
