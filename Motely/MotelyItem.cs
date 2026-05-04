using System.Runtime.CompilerServices;

namespace Motely;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct MotelyItem(int value) : IEquatable<MotelyItem>
{
    public int Value { get; } = value;

    public readonly MotelyItemType Type { get { return (MotelyItemType)(Value & MotelyGlobals.ItemTypeMask); } }
    public readonly MotelyItemTypeCategory TypeCategory { get { return (MotelyItemTypeCategory)(Value & MotelyGlobals.ItemTypeCategoryMask); } }
    public readonly MotelyItemSeal Seal { get { return (MotelyItemSeal)(Value & MotelyGlobals.ItemSealMask); } }
    public readonly MotelyItemEnhancement Enhancement { get { return (MotelyItemEnhancement)(Value & MotelyGlobals.ItemEnhancementMask); } }
    public readonly MotelyItemEdition Edition { get { return (MotelyItemEdition)(Value & MotelyGlobals.ItemEditionMask); } }

    public readonly MotelyStandardcardSuit StandardcardSuit { get { return (MotelyStandardcardSuit)(Value & MotelyGlobals.StandardcardSuitMask); } }
    public readonly MotelyStandardcardRank StandardcardRank { get { return (MotelyStandardcardRank)(Value & MotelyGlobals.StandardcardRankMask); } }

    public readonly bool IsPerishable { get { return (Value & (1 << MotelyGlobals.PerishableStickerOffset)) != 0; } }
    public readonly bool IsEternal { get { return (Value & (1 << MotelyGlobals.EternalStickerOffset)) != 0; } }
    public readonly bool IsRental { get { return (Value & (1 << MotelyGlobals.RentalStickerOffset)) != 0; } }

    public readonly bool IsInvalid { get { return TypeCategory == MotelyItemTypeCategory.Invalid; } }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem(MotelyItemType type)
        : this((int)type) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem(MotelyStandardCard card)
        : this((int)card | (int)MotelyItemTypeCategory.Standardcard) { }

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
    /// Parses a string produced by <see cref="FormatUtils.FormatItem"/>.
    /// Prefix order matches <see cref="FormatUtils.FormatItem"/> (stickers, seal, edition, enhancement, type).
    /// </summary>
    /// <exception cref="FormatException">Unrecognized layout or unknown type.</exception>
    public static MotelyItem Parse(string formatted) { return FormatUtils.ParseMotelyItem(formatted); }

    /// <inheritdoc cref="Parse"/>
    public static bool TryParse(string formatted, out MotelyItem item) { return FormatUtils.TryParseMotelyItem(formatted, out item); }

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
    public static bool operator ==(MotelyItem a, MotelyItem b) { return a.Equals(b); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(MotelyItem a, MotelyItem b) { return !a.Equals(b); }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public static implicit operator MotelyItem(MotelyItemType type) { return new(type); }
}
