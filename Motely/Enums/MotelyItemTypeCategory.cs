namespace Motely.Enums;

public enum MotelyItemTypeCategory
{
    Standardcard = 0b0001 << MotelyGlobals.ItemTypeCategoryOffset,
    SpectralCard = 0b0010 << MotelyGlobals.ItemTypeCategoryOffset,
    TarotCard = 0b0011 << MotelyGlobals.ItemTypeCategoryOffset,
    PlanetCard = 0b0100 << MotelyGlobals.ItemTypeCategoryOffset,
    Joker = 0b0101 << MotelyGlobals.ItemTypeCategoryOffset,
    Invalid = 0b1111 << MotelyGlobals.ItemTypeCategoryOffset,
}
