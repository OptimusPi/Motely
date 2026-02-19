namespace Motely;

public enum MotelyItemSeal
{
    None = 0b000 << MotelyCore.ItemSealOffset,
    Gold = 0b001 << MotelyCore.ItemSealOffset,
    Red = 0b010 << MotelyCore.ItemSealOffset,
    Blue = 0b011 << MotelyCore.ItemSealOffset,
    Purple = 0b100 << MotelyCore.ItemSealOffset,
}
