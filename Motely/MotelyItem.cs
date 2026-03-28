using System.Runtime.CompilerServices;

namespace Motely;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct MotelyItem(int value) : IEquatable<MotelyItem>
{
    public readonly int Value = value;

    public readonly MotelyItemType Type => (MotelyItemType)(Value & Motely.ItemTypeMask);
    public readonly MotelyItemTypeCategory TypeCategory =>
        (MotelyItemTypeCategory)(Value & Motely.ItemTypeCategoryMask);
    public readonly MotelyItemSeal Seal => (MotelyItemSeal)(Value & Motely.ItemSealMask);
    public readonly MotelyItemEnhancement Enhancement =>
        (MotelyItemEnhancement)(Value & Motely.ItemEnhancementMask);
    public readonly MotelyItemEdition Edition =>
        (MotelyItemEdition)(Value & Motely.ItemEditionMask);

    public readonly MotelyPlayingCardSuit PlayingCardSuit =>
        (MotelyPlayingCardSuit)(Value & Motely.PlayingCardSuitMask);
    public readonly MotelyPlayingCardRank PlayingCardRank =>
        (MotelyPlayingCardRank)(Value & Motely.PlayingCardRankMask);

    public readonly bool IsPerishable => (Value & (1 << Motely.PerishableStickerOffset)) != 0;
    public readonly bool IsEternal => (Value & (1 << Motely.EternalStickerOffset)) != 0;
    public readonly bool IsRental => (Value & (1 << Motely.RentalStickerOffset)) != 0;

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
        return new((Value & ~Motely.ItemTypeMask) | (int)type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithSeal(MotelyItemSeal seal)
    {
        return new((Value & ~Motely.ItemSealMask) | (int)seal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEnhancement(MotelyItemEnhancement enhancement)
    {
        return new((Value & ~Motely.ItemEnhancementMask) | (int)enhancement);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEdition(MotelyItemEdition edition)
    {
        return new((Value & ~Motely.ItemEditionMask) | (int)edition);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithPerishable(bool isPerishable)
    {
        int mask = 1 << Motely.PerishableStickerOffset;
        return new(isPerishable ? (Value | mask) : (Value & ~mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithEternal(bool isEternal)
    {
        int mask = 1 << Motely.EternalStickerOffset;
        return new(isEternal ? (Value | mask) : (Value & ~mask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItem WithRental(bool isRental)
    {
        int mask = 1 << Motely.RentalStickerOffset;
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
    /// Parses a <see cref="ToString"/>-shaped label (a &quot;jummy&quot;): prefix order matches
    /// <see cref="ToString"/> — <c>Seal Seal</c>, then stickers outer-to-inner
    /// <c>Rental</c> → <c>Eternal</c> → <c>Perishable</c>, then <c>Edition</c>, <c>Enhancement</c>, <c>Type</c>.
    /// </summary>
    /// <exception cref="FormatException">Unrecognized layout or unknown enum name.</exception>
    public static MotelyItem Parse(string jummy)
    {
        if (string.IsNullOrWhiteSpace(jummy))
            throw new FormatException("Motely item string is empty.");

        string str = jummy.Trim();

        MotelyItemSeal seal = MotelyItemSeal.None;
        foreach (MotelyItemSeal s in Enum.GetValues<MotelyItemSeal>())
        {
            if (s == MotelyItemSeal.None)
                continue;
            string prefix = s.ToString() + " Seal ";
            if (str.StartsWith(prefix, StringComparison.Ordinal))
            {
                seal = s;
                str = str[prefix.Length..].TrimStart();
                break;
            }
        }

        // Stickers are prepended in ToString in order Perishable → Eternal → Rental, so the
        // label reads outermost Rental, then Eternal, then Perishable before edition/enhancement/type.
        bool perishable = false;
        bool eternal = false;
        bool rental = false;
        if (str.StartsWith("Rental ", StringComparison.Ordinal))
        {
            rental = true;
            str = str["Rental ".Length..].TrimStart();
        }
        if (str.StartsWith("Eternal ", StringComparison.Ordinal))
        {
            eternal = true;
            str = str["Eternal ".Length..].TrimStart();
        }
        if (str.StartsWith("Perishable ", StringComparison.Ordinal))
        {
            perishable = true;
            str = str["Perishable ".Length..].TrimStart();
        }

        string[] parts = str.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new FormatException($"Missing item type in '{jummy}'.");

        MotelyItemEdition edition = MotelyItemEdition.None;
        MotelyItemEnhancement enhancement = MotelyItemEnhancement.None;
        MotelyItemType type;

        if (parts.Length == 1)
        {
            type = ParseType(parts[0], jummy);
        }
        else if (parts.Length == 2)
        {
            if (
                Enum.TryParse(parts[0], ignoreCase: true, out MotelyItemEdition ed)
                && ed != MotelyItemEdition.None
            )
            {
                edition = ed;
                type = ParseType(parts[1], jummy);
            }
            else if (
                Enum.TryParse(parts[0], ignoreCase: true, out MotelyItemEnhancement eh)
                && eh != MotelyItemEnhancement.None
            )
            {
                enhancement = eh;
                type = ParseType(parts[1], jummy);
            }
            else
                throw new FormatException($"Unrecognized motely item tail '{str}' (expected edition/type or enhancement/type).");
        }
        else if (parts.Length == 3)
        {
            if (!Enum.TryParse(parts[0], ignoreCase: true, out edition))
                throw new FormatException($"Unknown MotelyItemEdition '{parts[0]}' in '{jummy}'.");
            if (!Enum.TryParse(parts[1], ignoreCase: true, out enhancement))
                throw new FormatException($"Unknown MotelyItemEnhancement '{parts[1]}' in '{jummy}'.");
            type = ParseType(parts[2], jummy);
            if (edition == MotelyItemEdition.None || enhancement == MotelyItemEnhancement.None)
                throw new FormatException($"Invalid edition/enhancement pair in '{jummy}'.");
        }
        else
            throw new FormatException($"Too many tokens in '{jummy}'.");

        MotelyItem item = new(type);
        if (seal != MotelyItemSeal.None)
            item = item.WithSeal(seal);
        if (edition != MotelyItemEdition.None)
            item = item.WithEdition(edition);
        if (enhancement != MotelyItemEnhancement.None)
            item = item.WithEnhancement(enhancement);
        if (perishable)
            item = item.WithPerishable(true);
        if (eternal)
            item = item.WithEternal(true);
        if (rental)
            item = item.WithRental(true);
        return item;
    }

    private static MotelyItemType ParseType(string token, string originalJummy)
    {
        if (!Enum.TryParse<MotelyItemType>(token, ignoreCase: true, out MotelyItemType type))
            throw new FormatException($"Unknown MotelyItemType '{token}' in '{originalJummy}'.");
        return type;
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
