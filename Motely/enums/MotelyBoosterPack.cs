using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public enum MotelyBoosterPackType
{
    Arcana = 0,
    Celestial = 1,
    Standard = 2,
    Buffoon = 3,
    Spectral = 4,
}

public static class MotelyBoosterPackTypeExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetCardCount(this MotelyBoosterPackType type, MotelyBoosterPackSize size)
    {
        switch (type)
        {
            case MotelyBoosterPackType.Arcana:
            case MotelyBoosterPackType.Celestial:
            case MotelyBoosterPackType.Standard:
                return size switch
                {
                    MotelyBoosterPackSize.Normal => 3,
                    MotelyBoosterPackSize.Jumbo or MotelyBoosterPackSize.Mega => 5,
                    _ => throw new InvalidEnumArgumentException(),
                };
            case MotelyBoosterPackType.Buffoon:
            case MotelyBoosterPackType.Spectral:
                return size switch
                {
                    MotelyBoosterPackSize.Normal => 2,
                    MotelyBoosterPackSize.Jumbo or MotelyBoosterPackSize.Mega => 4,
                    _ => throw new InvalidEnumArgumentException(),
                };
            default:
                throw new InvalidEnumArgumentException();
        }
    }
}

public enum MotelyBoosterPackSize
{
    Normal = 0b00,
    Jumbo = 0b01,
    Mega = 0b10,
}

public enum MotelyBoosterPack
{
    Arcana =
        (MotelyBoosterPackType.Arcana << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboArcana =
        (MotelyBoosterPackType.Arcana << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaArcana =
        (MotelyBoosterPackType.Arcana << MotelyCore.BoosterPackTypeOffset) | MotelyBoosterPackSize.Mega,
    Celestial =
        (MotelyBoosterPackType.Celestial << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboCelestial =
        (MotelyBoosterPackType.Celestial << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaCelestial =
        (MotelyBoosterPackType.Celestial << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
    Standard =
        (MotelyBoosterPackType.Standard << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboStandard =
        (MotelyBoosterPackType.Standard << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaStandard =
        (MotelyBoosterPackType.Standard << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
    Buffoon =
        (MotelyBoosterPackType.Buffoon << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboBuffoon =
        (MotelyBoosterPackType.Buffoon << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaBuffoon =
        (MotelyBoosterPackType.Buffoon << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
    Spectral =
        (MotelyBoosterPackType.Spectral << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboSpectral =
        (MotelyBoosterPackType.Spectral << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaSpectral =
        (MotelyBoosterPackType.Spectral << MotelyCore.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
}

public static class MotelyBoosterPackExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyBoosterPackType GetPackType(this MotelyBoosterPack pack)
    {
        return (MotelyBoosterPackType)((int)pack >> MotelyCore.BoosterPackTypeOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorEnum256<MotelyBoosterPackType> GetPackType(
        this VectorEnum256<MotelyBoosterPack> packVector
    )
    {
        return new(packVector.HardwareVector >> MotelyCore.BoosterPackTypeOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyBoosterPackSize GetPackSize(this MotelyBoosterPack pack)
    {
        return (MotelyBoosterPackSize)((int)pack & MotelyCore.BoosterPackSizeMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorEnum256<MotelyBoosterPackSize> GetPackSize(
        this VectorEnum256<MotelyBoosterPack> packVector
    )
    {
        return new(packVector.HardwareVector & Vector256.Create(MotelyCore.BoosterPackSizeMask));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPackCardCount(this MotelyBoosterPack pack)
    {
        return pack.GetPackType().GetCardCount(pack.GetPackSize());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPackChoiceCount(this MotelyBoosterPack pack)
    {
        return pack.GetPackSize() switch
        {
            MotelyBoosterPackSize.Normal or MotelyBoosterPackSize.Jumbo => 1,
            MotelyBoosterPackSize.Mega => 2,
            _ => throw new InvalidEnumArgumentException(),
        };
    }
}

static partial class MotelyWeightedPools
{
    public static readonly MotelyWeightedPool<MotelyBoosterPack> BoosterPacks = new([
        new(MotelyBoosterPack.Arcana, 4),
        new(MotelyBoosterPack.JumboArcana, 2),
        new(MotelyBoosterPack.MegaArcana, 0.5),
        new(MotelyBoosterPack.Celestial, 4),
        new(MotelyBoosterPack.JumboCelestial, 2),
        new(MotelyBoosterPack.MegaCelestial, 0.5),
        new(MotelyBoosterPack.Standard, 4),
        new(MotelyBoosterPack.JumboStandard, 2),
        new(MotelyBoosterPack.MegaStandard, 0.5),
        new(MotelyBoosterPack.Buffoon, 1.2),
        new(MotelyBoosterPack.JumboBuffoon, 0.6),
        new(MotelyBoosterPack.MegaBuffoon, 0.15),
        new(MotelyBoosterPack.Spectral, 0.6),
        new(MotelyBoosterPack.JumboSpectral, 0.3),
        new(MotelyBoosterPack.MegaSpectral, 0.07),
    ]);
}
