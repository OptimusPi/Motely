namespace Motely;

public enum MotelyItemEnhancement
{
    None = 0b0000 << MotelyCore.ItemEnhancementOffset,
    Bonus = 0b0001 << MotelyCore.ItemEnhancementOffset,
    Mult = 0b0010 << MotelyCore.ItemEnhancementOffset,
    Wild = 0b0011 << MotelyCore.ItemEnhancementOffset,
    Glass = 0b0100 << MotelyCore.ItemEnhancementOffset,
    Steel = 0b0101 << MotelyCore.ItemEnhancementOffset,
    Stone = 0b0110 << MotelyCore.ItemEnhancementOffset,
    Gold = 0b0111 << MotelyCore.ItemEnhancementOffset,
    Lucky = 0b1000 << MotelyCore.ItemEnhancementOffset,
}
