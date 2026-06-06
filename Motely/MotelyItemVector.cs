using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct MotelyItemVector(Vector256<int> value)
{
    public static int Count => Vector256<int>.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Equals(MotelyItemVector a, MotelyItemVector b) =>
        Vector256.Equals(a.Value, b.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Equals(MotelyItemVector vector, MotelyItem item) =>
        Equals(vector, new MotelyItemVector(Vector256.Create(item.Value)));

    public readonly Vector256<int> Value = value;

    public readonly VectorEnum256<MotelyItemType> Type =>
        new(Vector256.BitwiseAnd(Value, Vector256.Create(MotelyGlobals.ItemTypeMask)));
    public readonly VectorEnum256<MotelyItemTypeCategory> TypeCategory =>
        new(Vector256.BitwiseAnd(Value, Vector256.Create(MotelyGlobals.ItemTypeCategoryMask)));
    public readonly VectorEnum256<MotelyItemSeal> Seal =>
        new(Vector256.BitwiseAnd(Value, Vector256.Create(MotelyGlobals.ItemSealMask)));
    public readonly VectorEnum256<MotelyItemEnhancement> Enhancement =>
        new(Vector256.BitwiseAnd(Value, Vector256.Create(MotelyGlobals.ItemEnhancementMask)));
    public readonly VectorEnum256<MotelyItemEdition> Edition =>
        new(Vector256.BitwiseAnd(Value, Vector256.Create(MotelyGlobals.ItemEditionMask)));

    public readonly VectorEnum256<MotelyStandardcardSuit> StandardcardSuit =>
        new(Vector256.BitwiseAnd(Value, Vector256.Create(MotelyGlobals.StandardcardSuitMask)));
    public readonly VectorEnum256<MotelyStandardcardRank> StandardcardRank =>
        new(Vector256.BitwiseAnd(Value, Vector256.Create(MotelyGlobals.StandardcardRankMask)));

    public readonly VectorMask IsPerishable =>
        ~Vector256.IsZero(
            Vector256.BitwiseAnd(
                Value,
                Vector256.Create(1 << MotelyGlobals.PerishableStickerOffset)
            )
        );
    public readonly VectorMask IsEternal =>
        ~Vector256.IsZero(
            Vector256.BitwiseAnd(Value, Vector256.Create(1 << MotelyGlobals.EternalStickerOffset))
        );
    public readonly VectorMask IsRental =>
        ~Vector256.IsZero(
            Vector256.BitwiseAnd(Value, Vector256.Create(1 << MotelyGlobals.RentalStickerOffset))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector(MotelyItem item)
        : this(Vector256.Create(item.Value)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector AsType(MotelyItemType type)
    {
        return new(
            Vector256.BitwiseOr(
                Vector256.BitwiseAnd(Value, Vector256.Create(~MotelyGlobals.ItemTypeMask)),
                Vector256.Create((int)type)
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithSeal(in VectorEnum256<MotelyItemSeal> edition)
    {
        return new(
            Vector256.BitwiseOr(
                Vector256.BitwiseAnd(Value, Vector256.Create(~MotelyGlobals.ItemSealMask)),
                edition.HardwareVector
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithSeal(MotelyItemSeal seal)
    {
        return WithSeal(VectorEnum256.Create(seal));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithEnhancement(in VectorEnum256<MotelyItemEnhancement> edition)
    {
        return new(
            Vector256.BitwiseOr(
                Vector256.BitwiseAnd(Value, Vector256.Create(~MotelyGlobals.ItemEnhancementMask)),
                edition.HardwareVector
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithEnhancement(MotelyItemEnhancement enhancement)
    {
        return WithEnhancement(VectorEnum256.Create(enhancement));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithEdition(in VectorEnum256<MotelyItemEdition> edition)
    {
        return new(
            Vector256.BitwiseOr(
                Vector256.BitwiseAnd(Value, Vector256.Create(~MotelyGlobals.ItemEditionMask)),
                edition.HardwareVector
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithEdition(MotelyItemEdition edition)
    {
        return WithEdition(VectorEnum256.Create(edition));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithPerishable(in Vector256<int> isPerishable)
    {
        Vector256<int> mask = Vector256.Create(1 << MotelyGlobals.PerishableStickerOffset);
        return new(
            Vector256.BitwiseOr(
                Vector256.BitwiseAnd(Value, ~mask),
                Vector256.BitwiseAnd(mask, isPerishable)
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithPerishable(bool isPerishable)
    {
        int mask = 1 << MotelyGlobals.PerishableStickerOffset;
        return new(
            isPerishable
                ? Vector256.BitwiseOr(Value, Vector256.Create(mask))
                : Vector256.BitwiseAnd(Value, Vector256.Create(~mask))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithEternal(in Vector256<int> isEternal)
    {
        Vector256<int> mask = Vector256.Create(1 << MotelyGlobals.EternalStickerOffset);
        return new(
            Vector256.BitwiseOr(
                Vector256.BitwiseAnd(Value, ~mask),
                Vector256.BitwiseAnd(mask, isEternal)
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithEternal(bool isEternal)
    {
        int mask = 1 << MotelyGlobals.EternalStickerOffset;
        return new(
            isEternal
                ? Vector256.BitwiseOr(Value, Vector256.Create(mask))
                : Vector256.BitwiseAnd(Value, Vector256.Create(~mask))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithRental(in Vector256<int> isRental)
    {
        Vector256<int> mask = Vector256.Create(1 << MotelyGlobals.RentalStickerOffset);
        return new(
            Vector256.BitwiseOr(
                Vector256.BitwiseAnd(Value, ~mask),
                Vector256.BitwiseAnd(mask, isRental)
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MotelyItemVector WithRental(bool isRental)
    {
        int mask = 1 << MotelyGlobals.RentalStickerOffset;
        return new(
            isRental
                ? Vector256.BitwiseOr(Value, Vector256.Create(mask))
                : Vector256.BitwiseAnd(Value, Vector256.Create(~mask))
        );
    }

    public MotelyItem this[int i]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { return new(Value[i]); }
    }

    public override string ToString()
    {
        return $"<{this[0]}, {this[1]}, {this[2]}, {this[3]}, {this[4]}, {this[5]}, {this[6]}, {this[7]}>";
    }
}
