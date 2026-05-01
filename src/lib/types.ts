// Balatro deck types
export type DeckType =
    | 'red' | 'blue' | 'yellow' | 'green' | 'black'
    | 'magic' | 'nebula' | 'ghost' | 'abandoned' | 'checkered'
    | 'zodiac' | 'painted' | 'anaglyph' | 'plasma' | 'erratic';

// Balatro stake types
export type StakeType =
    | 'white' | 'red' | 'green' | 'black'
    | 'blue' | 'purple' | 'orange' | 'gold';

/**
 * Configuration for a Daily Ritual challenge
 * Matches the dailyritual.schema.json structure
 */
export interface RitualConfig {
    filterId: string;       // e.g. "wee_joker_daily"
    searchId: string;       // e.g. "wee_joker_daily__erratic_white"
    deck: DeckType;
    stake: StakeType;
    seeds: string;          // Path to seeds file
    name: string;           // Display name
    author: string;         // Curator username
    description?: string;   // Brief description
    tutorial?: string;      // How to use this seed
    epoch?: string;         // ISO date-time for Day 1
    icon?: string;          // Emoji for the ritual
    color?: string;         // Theme color (CSS value)
}

/**
 * Seed data record from the seeds file.
 * All scoring fields are dynamic — driven by the JAML filter's label columns.
 */
export interface SeedData {
    seed: string;
    score: number;
    stake?: StakeType;
    startingDeck?: string[];
    relevantEvents?: RelevantEvent[];
    [key: string]: string | number | boolean | string[] | RelevantEvent[] | undefined;
}

export interface ParsedCard {
    rank: string;
    suit: string;
    enhancement: string | null;
    seal: string | null;
    edition: string | null;
}

// ======================================================================
// JAMZ Schema Types (Daily Ritual Engine)
// These types define the structure of data exported from Motely CLI
// ======================================================================

/**
 * The type of a game event/item
 */
export type RelevantEventType = 'joker' | 'tarot' | 'spectral' | 'planet' | 'voucher';

/**
 * The source/location where the event occurs in game
 */
export type EventSource =
    | 'shop'           // Regular shop purchase
    | 'arcana_pack'    // Arcana pack opening
    | 'spectral_pack'  // Spectral pack opening
    | 'celestial_pack' // Celestial pack opening
    | 'buffoon_pack'   // Buffoon (Joker) pack opening
    | 'standard_pack'  // Standard pack opening
    | 'tag'            // Tag reward
    | 'boss_blind'     // Boss blind reward
    | 'voucher';       // Voucher slot

/**
 * A "relevant" event as defined by the JAML filter's Should rules.
 * Represents anything the ritual cares about tracking (jokers, consumables, etc.)
 */
export interface RelevantEvent {
    /** The ante number (1, 2, 3, ...) when this event occurs */
    ante: number;

    /** Where this event occurs (shop, pack, tag, etc.) */
    source: EventSource;

    /** The category of item */
    type: RelevantEventType;

    /** The item's identifier (e.g., "weejoker", "temperance", "blueprint") */
    id: string;

    /** Optional edition modifier (e.g., "negative", "polychrome", "foil", "holographic") */
    edition?: string;

    /** Optional enhancement (for standard cards: "bonus", "mult", "wild", etc.) */
    enhancement?: string;

    /** Optional seal (for standard cards: "gold", "red", "blue", "purple") */
    seal?: string;

    /** Count of this item (defaults to 1 if not specified) */
    count?: number;

    /** Optional display name override */
    displayName?: string;
}

/**
 * Complete seed data as exported in a JAMZ file.
 * This is the "rich" data format containing everything needed to render the UI.
 */
export interface JamzSeedData {
    /** The 8-character Balatro seed string */
    seed: string;

    /** Calculated "goodness" score from the JAML filter */
    score: number;

    /** Count of Rank 2s in the starting deck (for Erratic deck display) */
    twos?: number;

    /** The exact 52 cards in starting deck. Format: {rank}_{suit} e.g., ["2_C", "10_H", "A_S"] */
    startingDeck?: string[];

    /** All relevant events matching the JAML filter's Should rules */
    relevantEvents?: RelevantEvent[];

    /** Legacy: individual JAML column values (for backward compatibility) */
    [key: string]: string | number | boolean | string[] | RelevantEvent[] | undefined;
}

/**
 * Header metadata in a JAMZ file
 */
export interface JamzHeader {
    /** JAMZ format version (e.g., "1") */
    version: string;

    /** Hash of the source JAML filter for verification */
    jamlHash: string;

    /** Total number of seeds in this file */
    seedCount: number;

    /** ISO 8601 timestamp when this file was generated */
    generatedAt: string;

    /** Ritual metadata */
    ritual: {
        id: string;
        name: string;
        author: string;
        description?: string;
        tutorial?: string;
        deck: DeckType;
        stake: StakeType;
        icon?: string;
        color?: string;
    };
}

/**
 * Complete JAMZ file structure (after decompression and JSON parsing)
 */
export interface JamzFile {
    header: JamzHeader;
    seeds: JamzSeedData[];
}

