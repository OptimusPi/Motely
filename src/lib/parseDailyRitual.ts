/**
 * Parser for daily_ritual.json abbreviated format
 * 
 * Expands abbreviated fields like "wj2", "hc1" into full item objects
 */

export interface ParsedItem {
    id: string;
    name: string;
    type: 'joker' | 'consumable' | 'voucher';
    ante: number;
}

export interface ParsedSeed {
    seed: string;
    title: string;
    score: number;
    twos: number;
    items: ParsedItem[];
}

// Mapping of abbreviations to full item data
const ITEM_MAP: Record<string, { id: string; name: string; type: 'joker' | 'consumable' | 'voucher' }> = {
    wj: { id: 'weejoker', name: 'Wee Joker', type: 'joker' },
    hc: { id: 'hangingchad', name: 'Hanging Chad', type: 'joker' },
    hk: { id: 'hack', name: 'Hack', type: 'joker' },
    bp: { id: 'blueprint', name: 'Blueprint', type: 'joker' },
    bs: { id: 'brainstorm', name: 'Brainstorm', type: 'joker' },
    sh: { id: 'showman', name: 'Showman', type: 'joker' },
    cp: { id: 'copy', name: 'Copy', type: 'consumable' }, // Placeholder
    // Add more as needed
};

/**
 * Parse daily_ritual.json seed format
 * 
 * Example input:
 * {"id":"J4SPZMWW","t":"Twosday","j":"Joker","s":206,"w":16,"wj2":1,"hc2":1}
 * 
 * Output:
 * {
 *   seed: "J4SPZMWW",
 *   title: "Twosday",
 *   score: 206,
 *   twos: 16,
 *   items: [
 *     { id: 'weejoker', name: 'Wee Joker', type: 'joker', ante: 2 },
 *     { id: 'hangingchad', name: 'Hanging Chad', type: 'joker', ante: 2 }
 *   ]
 * }
 */
export function parseDailyRitualSeed(raw: any): ParsedSeed {
    const items: ParsedItem[] = [];

    // Parse each field looking for pattern: letters + number (e.g., "wj2", "hc1")
    Object.keys(raw).forEach(key => {
        const match = key.match(/^([a-z]+)(\d+)$/);
        if (match) {
            const [, code, ante] = match;
            const itemData = ITEM_MAP[code];

            if (itemData) {
                const count = raw[key]; // Could be used for duplicates
                items.push({
                    ...itemData,
                    ante: Number(ante)
                });
            }
        }
    });

    return {
        seed: raw.id || '',
        title: raw.t || '',
        score: raw.s || 0,
        twos: raw.w || 0,
        items
    };
}

/**
 * Group items by type for rendering
 */
export function groupItemsByType(items: ParsedItem[]) {
    return {
        jokers: items.filter(i => i.type === 'joker'),
        consumables: items.filter(i => i.type === 'consumable'),
        vouchers: items.filter(i => i.type === 'voucher')
    };
}
