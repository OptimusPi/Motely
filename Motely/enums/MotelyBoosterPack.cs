using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely.Enums;

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
        (MotelyBoosterPackType.Arcana << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboArcana =
        (MotelyBoosterPackType.Arcana << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaArcana =
        (MotelyBoosterPackType.Arcana << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
    Celestial =
        (MotelyBoosterPackType.Celestial << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboCelestial =
        (MotelyBoosterPackType.Celestial << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaCelestial =
        (MotelyBoosterPackType.Celestial << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
    Standard =
        (MotelyBoosterPackType.Standard << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboStandard =
        (MotelyBoosterPackType.Standard << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaStandard =
        (MotelyBoosterPackType.Standard << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
    Buffoon =
        (MotelyBoosterPackType.Buffoon << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboBuffoon =
        (MotelyBoosterPackType.Buffoon << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaBuffoon =
        (MotelyBoosterPackType.Buffoon << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
    Spectral =
        (MotelyBoosterPackType.Spectral << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Normal,
    JumboSpectral =
        (MotelyBoosterPackType.Spectral << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Jumbo,
    MegaSpectral =
        (MotelyBoosterPackType.Spectral << MotelyGlobals.BoosterPackTypeOffset)
        | MotelyBoosterPackSize.Mega,
}

public static class MotelyBoosterPackExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyBoosterPackType GetPackType(this MotelyBoosterPack pack)
    {
        return (MotelyBoosterPackType)((int)pack >> MotelyGlobals.BoosterPackTypeOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorEnum256<MotelyBoosterPackType> GetPackType(
        this VectorEnum256<MotelyBoosterPack> packVector
    )
    {
        return new(packVector.HardwareVector >> MotelyGlobals.BoosterPackTypeOffset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyBoosterPackSize GetPackSize(this MotelyBoosterPack pack)
    {
        return (MotelyBoosterPackSize)((int)pack & MotelyGlobals.BoosterPackSizeMask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VectorEnum256<MotelyBoosterPackSize> GetPackSize(
        this VectorEnum256<MotelyBoosterPack> packVector
    )
    {
        return new(packVector.HardwareVector & Vector256.Create(MotelyGlobals.BoosterPackSizeMask));
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
