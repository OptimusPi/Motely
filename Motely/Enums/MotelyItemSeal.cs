namespace Motely.Enums;

public enum MotelyItemSeal
{
    None = 0b000 << MotelyGlobals.ItemSealOffset,
    Gold = 0b001 << MotelyGlobals.ItemSealOffset,
    Red = 0b010 << MotelyGlobals.ItemSealOffset,
    Blue = 0b011 << MotelyGlobals.ItemSealOffset,
    Purple = 0b100 << MotelyGlobals.ItemSealOffset,
}
