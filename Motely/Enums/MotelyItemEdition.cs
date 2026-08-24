namespace Motely.Enums;

public enum MotelyItemEdition
{
    None = 0b000 << MotelyGlobals.ItemEditionOffset,
    Foil = 0b001 << MotelyGlobals.ItemEditionOffset,
    Holographic = 0b010 << MotelyGlobals.ItemEditionOffset,
    Polychrome = 0b011 << MotelyGlobals.ItemEditionOffset,
    Negative = 0b100 << MotelyGlobals.ItemEditionOffset,
}
