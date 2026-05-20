namespace Motely.Enums;

public enum MotelyItemEnhancement
{
    None = 0b0000 << MotelyGlobals.ItemEnhancementOffset,
    Bonus = 0b0001 << MotelyGlobals.ItemEnhancementOffset,
    Mult = 0b0010 << MotelyGlobals.ItemEnhancementOffset,
    Wild = 0b0011 << MotelyGlobals.ItemEnhancementOffset,
    Glass = 0b0100 << MotelyGlobals.ItemEnhancementOffset,
    Steel = 0b0101 << MotelyGlobals.ItemEnhancementOffset,
    Stone = 0b0110 << MotelyGlobals.ItemEnhancementOffset,
    Gold = 0b0111 << MotelyGlobals.ItemEnhancementOffset,
    Lucky = 0b1000 << MotelyGlobals.ItemEnhancementOffset,
}
