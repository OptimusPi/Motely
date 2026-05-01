import { jokers, tarotsAndPlanets, vouchers, boosterPacks, editionMap, stickerMap, jokerFaces, consumablesFaces, bosses, tags } from './const';

interface SpritePos {
    x: number;
    y: number;
}

export interface SpriteData {
    pos: SpritePos;
    type: 'Jokers' | 'Tarots' | 'Vouchers' | 'Boosters' | 'Enhancers' | 'Editions' | 'BlindChips' | 'Shop' | 'tags';
}

// Consolidate all item arrays into a single map for O(1) lookup
const ITEM_MAP = new Map<string, SpriteData>();

function addToMap(items: any[], type: SpriteData['type']) {
    items.forEach(item => {
        if (item.name && item.pos) {
            const data: SpriteData = { pos: item.pos, type };
            ITEM_MAP.set(item.name, data);
            ITEM_MAP.set(item.name.toLowerCase(), data);
            ITEM_MAP.set(item.name.replace(/ /g, ''), data);
            ITEM_MAP.set(item.name.replace(/ /g, '').toLowerCase(), data);
        }
    });
}

// Populate the map with explicit types
addToMap(jokers, 'Jokers');
addToMap(tarotsAndPlanets, 'Tarots');
addToMap(vouchers, 'Vouchers');
addToMap(boosterPacks, 'Boosters');
addToMap(bosses, 'BlindChips');
addToMap(tags, 'tags');

// Explicit overrides/faces
addToMap(jokerFaces, 'Jokers');
addToMap(consumablesFaces, 'Tarots');


export function getSpriteData(name: string): SpriteData {
    // 1. Try stripping common prefixes used by some analyzers (e.g., "Planet | Venus")
    const cleanedName = name.replace(/^(Joker|Tarot|Planet|Voucher|Pack|Edition|Tag) [|:] /i, "").trim();

    // 2. Precise lookup
    if (ITEM_MAP.has(cleanedName)) return ITEM_MAP.get(cleanedName)!;
    if (ITEM_MAP.has(name)) return ITEM_MAP.get(name)!;

    // 3. Normalized lookups
    const variants = [
        cleanedName.toLowerCase(),
        cleanedName.replace(/ /g, ''),
        cleanedName.replace(/ /g, '').toLowerCase(),
        name.toLowerCase(),
        name.replace(/ /g, ''),
        name.replace(/ /g, '').toLowerCase()
    ];

    for (const v of variants) {
        if (ITEM_MAP.has(v)) return ITEM_MAP.get(v)!;
    }

    // Default Fallback
    return { pos: { x: 0, y: 0 }, type: 'Jokers' };
}
