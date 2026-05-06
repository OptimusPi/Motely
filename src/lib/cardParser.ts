export interface ParsedCard {
    rank: string;
    suit: string;
    enhancement: string | null;
    seal: string | null;
    edition: string | null;
}

const ENHANCEMENTS = ["Bonus", "Mult", "Wild", "Lucky", "Glass", "Steel", "Stone", "Gold"];
const SEALS = ["Gold", "Purple", "Red", "Blue"];
const EDITIONS = ["Foil", "Holographic", "Polychrome", "Negative"];


// Internal Balatro short codes
const RANK_MAP: Record<string, string> = {
    "2": "2", "3": "3", "4": "4", "5": "5", "6": "6", "7": "7", "8": "8", "9": "9", "10": "10",
    "T": "10", "J": "Jack", "Q": "Queen", "K": "King", "A": "Ace"
};
const SUIT_MAP: Record<string, string> = {
    "H": "Hearts", "C": "Clubs", "D": "Diamonds", "S": "Spades"
};

export function parseCardToken(item: unknown): ParsedCard | null {
    if (!item) return null;

    // 1. If it's already an object with rank/suit
    if (typeof item === 'object') {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const obj = item as Record<string, any>;
        if (obj.rank || obj.base) {
            let rank = obj.rank || obj.base?.[2] || obj.base?.[0]; // Handle different analyzer versions
            let suit = obj.suit || obj.base?.[0];

            // Normalize
            rank = RANK_MAP[rank] || rank;
            suit = SUIT_MAP[suit] || suit;
            if (suit && !suit.endsWith('s')) suit += 's';

            return {
                rank: rank || "Ace",
                suit: suit || "Spades",
                enhancement: obj.enhancement || obj.modifier || null,
                seal: obj.seal || null,
                edition: obj.edition || null
            };
        }
    }

    // 2. If it's a string token
    if (typeof item === 'string') {
        const str = item.trim();

        // Handle rank_suit format e.g. "2_C"
        if (str.includes('_')) {
            const [r, s] = str.split('_');
            const rank = RANK_MAP[r.toUpperCase()] || r;
            const suit = SUIT_MAP[s.toUpperCase()] || s;
            return { rank, suit, enhancement: null, seal: null, edition: null };
        }

        // Handle "Rank of Suit" format
        const parts = str.toLowerCase().split(' ');
        const ofIndex = parts.indexOf('of');
        if (ofIndex > 0 && ofIndex < parts.length - 1) {
            let rank = parts[ofIndex - 1];
            let suit = parts[ofIndex + 1];

            // Normalize
            rank = rank.charAt(0).toUpperCase() + rank.slice(1);
            suit = suit.charAt(0).toUpperCase() + suit.slice(1);
            if (!suit.endsWith('s')) suit += 's';

            return {
                rank: RANK_MAP[rank.toUpperCase()] || rank,
                suit: SUIT_MAP[suit.toUpperCase()] || suit,
                enhancement: parts.find(p => ENHANCEMENTS.some(e => e.toLowerCase() === p)) || null,
                seal: parts.find(p => SEALS.some(s => s.toLowerCase() === p)) || null,
                edition: parts.find(p => EDITIONS.some(e => e.toLowerCase() === p)) || null
            };
        }
    }

    return null;
}
