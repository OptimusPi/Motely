namespace Motely;

public enum MotelyItemEdition
{
    None = 0b000 << MotelyCore.ItemEditionOffset,
    Foil = 0b001 << MotelyCore.ItemEditionOffset,
    Holographic = 0b010 << MotelyCore.ItemEditionOffset,
    Polychrome = 0b011 << MotelyCore.ItemEditionOffset,
    Negative = 0b100 << MotelyCore.ItemEditionOffset,
}
