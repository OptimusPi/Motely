/** Bit-packed shop/card ids (Balatro item encoding). */

export const BalatroItemCategory = {
    Standardcard: 1,
    Spectral: 2,
    Tarot: 3,
    Planet: 4,
    Joker: 5,
    Invalid: 0xf,
} as const;

const CATEGORY_OFFSET = 12;
const CATEGORY_MASK = 0xf000;
const RARITY_OFFSET = 10;
const RARITY_MASK = 0x0c00;

export function packedItemCategory(packed: number): number {
    return (packed & CATEGORY_MASK) >> CATEGORY_OFFSET;
}

export function packedJokerRarity(packed: number): number {
    return (packed & RARITY_MASK) >> RARITY_OFFSET;
}

export function packedItemIndex(packed: number): number {
    return packed & ~(CATEGORY_MASK | RARITY_MASK);
}

export function isPackedItemValid(packed: number): boolean {
    const category = packedItemCategory(packed);
    return category >= BalatroItemCategory.Standardcard && category <= BalatroItemCategory.Joker;
}
