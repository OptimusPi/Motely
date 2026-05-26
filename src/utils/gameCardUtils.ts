import { RANK_MAP, SUIT_MAP, SEAL_MAP, ENHANCER_MAP, type SpritePos } from "../sprites/spriteData.js";

export function getStandardCardPosition(rank: string, suit: string): SpritePos {
    return { x: RANK_MAP[rank] ?? 0, y: SUIT_MAP[suit] ?? 0 };
}

export function getSealPosition(seal: string): SpritePos | undefined {
    return SEAL_MAP[seal];
}

export function getEnhancerPosition(modifiers: string[]): SpritePos {
    for (const m of modifiers) {
        const pos = ENHANCER_MAP[m];
        if (pos) return pos;
    }
    return { x: 1, y: 0 };
}
