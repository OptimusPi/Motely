namespace Motely;

public enum MotelyItemTypeCategory
{
    PlayingCard = 0b0001 << MotelyCore.ItemTypeCategoryOffset,
    SpectralCard = 0b0010 << MotelyCore.ItemTypeCategoryOffset,
    TarotCard = 0b0011 << MotelyCore.ItemTypeCategoryOffset,
    PlanetCard = 0b0100 << MotelyCore.ItemTypeCategoryOffset,
    Joker = 0b0101 << MotelyCore.ItemTypeCategoryOffset,
    Invalid = 0b1111 << MotelyCore.ItemTypeCategoryOffset,
}
