"use client";

import React from "react";
import { Layer } from "../render/Layer.js";
import { JamlCardRenderer } from "../render/CanvasRenderer.js";
import {
    JOKERS,
    JOKER_FACES,
    TAROTS_AND_PLANETS,
    CONSUMABLE_FACES,
    TAGS,
    VOUCHERS,
    BOSSES,
    EDITION_MAP,
    SPRITE_SHEETS,
    STICKER_MAP,
    RANK_MAP,
    SUIT_MAP,
    ENHANCER_MAP,
    SEAL_MAP,
} from "../sprites/spriteData.js";

export interface JamlGameCardProps {
    card: {
        name: string;
        edition?: "Foil" | "Holographic" | "Polychrome" | "Negative";
        isEternal?: boolean;
        isPerishable?: boolean;
        isRental?: boolean;
        rank?: string;
        suit?: string;
        enhancements?: string[];
        seal?: string;
        scale?: number;
    };
    type: "joker" | "consumable" | "playing";
    className?: string;
    hoverTilt?: boolean;
}

export type AnalyzerShopItem = {
    id: string;
    name: string;
    value?: number;
};

export type AnalyzerResolvedItem =
    | { kind: "voucher"; voucherName: string }
    | {
        kind: "joker" | "consumable" | "playing";
        card: JamlGameCardProps["card"];
        type: JamlGameCardProps["type"];
    }
    | { kind: "unknown"; label: string };

function normalizeCardRank(raw: string): string {
    const value = raw.trim().toUpperCase();
    if (value === "A" || value === "ACE") return "Ace";
    if (value === "K" || value === "KING") return "King";
    if (value === "Q" || value === "QUEEN") return "Queen";
    if (value === "J" || value === "JACK") return "Jack";
    return raw.trim();
}

function normalizeCardSuit(raw: string): string {
    const value = raw.trim().toLowerCase();
    if (value === "heart" || value === "hearts") return "Hearts";
    if (value === "club" || value === "clubs") return "Clubs";
    if (value === "diamond" || value === "diamonds") return "Diamonds";
    if (value === "spade" || value === "spades") return "Spades";
    return raw.trim();
}

function parseStandardcardName(name: string): { rank: string; suit: string } | null {
    const trimmed = name.trim();
    const ofMatch = /^(A|K|Q|J|10|[2-9]|Ace|King|Queen|Jack)\s+of\s+(Hearts|Clubs|Diamonds|Spades)$/i.exec(trimmed);
    if (ofMatch) {
        return {
            rank: normalizeCardRank(ofMatch[1]),
            suit: normalizeCardSuit(ofMatch[2]),
        };
    }

    const shortMatch = /^(A|K|Q|J|10|[2-9])\s*([HCDS])$/i.exec(trimmed);
    if (shortMatch) {
        const suitMap: Record<string, string> = {
            H: "Hearts",
            C: "Clubs",
            D: "Diamonds",
            S: "Spades",
        };
        return {
            rank: normalizeCardRank(shortMatch[1]),
            suit: suitMap[shortMatch[2].toUpperCase()],
        };
    }

    return null;
}

const MODIFIER_PREFIXES = [
    "Eternal",
    "Perishable",
    "Rental",
    "Foil",
    "Holographic",
    "Polychrome",
    "Negative",
    "Bonus",
    "Mult",
    "Wild",
    "Glass",
    "Steel",
    "Stone",
    "Gold",
    "Lucky",
    "Gold Seal",
    "Red Seal",
    "Blue Seal",
    "Purple Seal",
];

function stripModifiers(name: string): {
    baseName: string;
    edition?: "Foil" | "Holographic" | "Polychrome" | "Negative";
    isEternal?: boolean;
    isPerishable?: boolean;
    isRental?: boolean;
} {
    let remaining = name;
    let edition: "Foil" | "Holographic" | "Polychrome" | "Negative" | undefined;
    let isEternal = false;
    let isPerishable = false;
    let isRental = false;

    let changed = true;
    while (changed) {
        changed = false;
        for (const prefix of MODIFIER_PREFIXES) {
            if (remaining.startsWith(prefix + " ")) {
                const stripped = remaining.slice(prefix.length + 1);
                if (prefix === "Foil" || prefix === "Holographic" || prefix === "Polychrome" || prefix === "Negative") {
                    edition = prefix as typeof edition;
                } else if (prefix === "Eternal") {
                    isEternal = true;
                } else if (prefix === "Perishable") {
                    isPerishable = true;
                } else if (prefix === "Rental") {
                    isRental = true;
                }
                remaining = stripped;
                changed = true;
                break;
            }
        }
    }

    return { baseName: remaining, edition, isEternal, isPerishable, isRental };
}

function resolvePackedAnalyzerItem(item: AnalyzerShopItem, scale: number): AnalyzerResolvedItem | null {
    if (typeof item.value !== "number" || !Number.isFinite(item.value)) {
        return null;
    }

    const displayName = String(item.name || "").trim();
    const { baseName, edition, isEternal, isPerishable, isRental } = stripModifiers(displayName);

    // Use motely-wasm enum to determine category — no hand-rolled bitmasks
    const itemType = item.value & 0xffff;
    const catNibble = (itemType >> 12) & 0xf;

    if (catNibble === 5 /* Joker */) {
        const jokerName = JOKERS.some((joker) => joker.name === baseName) ? baseName : displayName;
        if (JOKERS.some((joker) => joker.name === jokerName)) {
            return { kind: "joker", type: "joker", card: { name: jokerName, edition, isEternal, isPerishable, isRental, scale } };
        }
    }

    if (
        catNibble === 3 /* Tarot */ ||
        catNibble === 4 /* Planet */ ||
        catNibble === 2 /* Spectral */
    ) {
        const consumableName = TAROTS_AND_PLANETS.some((consumable) => consumable.name === baseName) ? baseName : displayName;
        if (TAROTS_AND_PLANETS.some((consumable) => consumable.name === consumableName)) {
            return { kind: "consumable", type: "consumable", card: { name: consumableName, edition, scale } };
        }
    }

    if (baseName !== displayName) {
        if (JOKERS.some((joker) => joker.name === baseName)) {
            return { kind: "joker", type: "joker", card: { name: baseName, edition, isEternal, isPerishable, isRental, scale } };
        }
        if (TAROTS_AND_PLANETS.some((consumable) => consumable.name === baseName)) {
            return { kind: "consumable", type: "consumable", card: { name: baseName, edition, scale } };
        }
        if (VOUCHERS.some((voucher) => voucher.name === baseName)) {
            return { kind: "voucher", voucherName: baseName };
        }
    }

    const standardcard = parseStandardcardName(displayName) ?? parseStandardcardName(baseName);
    if (standardcard) {
        return {
            kind: "playing",
            type: "playing",
            card: {
                name: displayName,
                rank: standardcard.rank,
                suit: standardcard.suit,
                scale,
            },
        };
    }

    return { kind: "unknown", label: displayName };
}

// eslint-disable-next-line react-refresh/only-export-components
export function resolveAnalyzerShopItem(item: AnalyzerShopItem, scale = 1): AnalyzerResolvedItem {
    const displayName = String(item.name || "").trim();

    if (!displayName) {
        return { kind: "unknown", label: String(item.id || "").trim() || "Unknown Item" };
    }

    const packedResolved = resolvePackedAnalyzerItem(item, scale);
    if (packedResolved && packedResolved.kind !== "unknown") {
        return packedResolved;
    }

    if (VOUCHERS.some((voucher) => voucher.name === displayName)) {
        return { kind: "voucher", voucherName: displayName };
    }

    if (JOKERS.some((joker) => joker.name === displayName)) {
        return { kind: "joker", type: "joker", card: { name: displayName, scale } };
    }

    if (TAROTS_AND_PLANETS.some((consumable) => consumable.name === displayName)) {
        return { kind: "consumable", type: "consumable", card: { name: displayName, scale } };
    }

    const { baseName, edition, isEternal, isPerishable, isRental } = stripModifiers(displayName);

    if (baseName !== displayName) {
        if (JOKERS.some((joker) => joker.name === baseName)) {
            return { kind: "joker", type: "joker", card: { name: baseName, edition, isEternal, isPerishable, isRental, scale } };
        }
        if (TAROTS_AND_PLANETS.some((consumable) => consumable.name === baseName)) {
            return { kind: "consumable", type: "consumable", card: { name: baseName, edition, scale } };
        }
        if (VOUCHERS.some((voucher) => voucher.name === baseName)) {
            return { kind: "voucher", voucherName: baseName };
        }
    }

    const standardcard = parseStandardcardName(displayName) ?? parseStandardcardName(baseName);
    if (standardcard) {
        return {
            kind: "playing",
            type: "playing",
            card: {
                name: displayName,
                rank: standardcard.rank,
                suit: standardcard.suit,
                scale,
            },
        };
    }

    return packedResolved ?? { kind: "unknown", label: displayName };
}

export function JamlGameCard({ card, type, className = "", hoverTilt = false }: JamlGameCardProps) {
    const { name, edition, isEternal, isPerishable, isRental, rank, suit, enhancements, seal, scale = 1 } = card;
    const layers: Layer[] = [];

    if (type === "joker") {
        const jokerData = JOKERS.find((j) => j.name === name);
        if (jokerData) layers.push(new Layer({ ...jokerData, source: SPRITE_SHEETS.jokers.src, order: 0, columns: SPRITE_SHEETS.jokers.columns, rows: SPRITE_SHEETS.jokers.rows }));
        const face = JOKER_FACES.find((j) => j.name === name);
        if (face) layers.push(new Layer({ ...face, source: SPRITE_SHEETS.jokers.src, order: 1, columns: SPRITE_SHEETS.jokers.columns, rows: SPRITE_SHEETS.jokers.rows }));
    } else if (type === "consumable") {
        const consumable = TAROTS_AND_PLANETS.find((t) => t.name === name);
        if (consumable) layers.push(new Layer({ ...consumable, order: 0, source: SPRITE_SHEETS.tarots.src, rows: SPRITE_SHEETS.tarots.rows, columns: SPRITE_SHEETS.tarots.columns }));
        const face = CONSUMABLE_FACES.find((t) => t.name === name);
        if (face)
            layers.push(
                new Layer({
                    ...face,
                    order: 1,
                    source: SPRITE_SHEETS.enhancers.src,
                    rows: SPRITE_SHEETS.enhancers.rows,
                    columns: SPRITE_SHEETS.enhancers.columns,
                    animated: face.animated,
                }),
            );
    } else if (rank && suit) {
        const enhancerPos = (enhancements ?? [])
            .map((m) => ENHANCER_MAP[m])
            .find((pos): pos is { x: number; y: number } => Boolean(pos)) ?? { x: 1, y: 0 };
        layers.push(
            new Layer({
                pos: enhancerPos,
                name: "background",
                order: 0,
                source: SPRITE_SHEETS.enhancers.src,
                rows: SPRITE_SHEETS.enhancers.rows,
                columns: SPRITE_SHEETS.enhancers.columns,
            }),
        );
        layers.push(
            new Layer({
                pos: { x: RANK_MAP[rank] ?? 0, y: SUIT_MAP[suit] ?? 0 },
                name,
                order: 1,
                source: SPRITE_SHEETS.deck.src,
                rows: SPRITE_SHEETS.deck.rows,
                columns: SPRITE_SHEETS.deck.columns,
            }),
        );
    }

    if (edition) {
        const index = EDITION_MAP[edition];
        if (index !== undefined) {
            layers.push(
                new Layer({
                    pos: { x: index, y: 0 },
                    name: edition,
                    order: 2,
                    source: SPRITE_SHEETS.editions.src,
                    rows: SPRITE_SHEETS.editions.rows,
                    columns: SPRITE_SHEETS.editions.columns,
                }),
            );
        }
    }

    if (isEternal) {
        layers.push(
            new Layer({
                pos: STICKER_MAP["Eternal"],
                name: "Eternal",
                order: 3,
                source: SPRITE_SHEETS.stickers.src,
                rows: SPRITE_SHEETS.stickers.rows,
                columns: SPRITE_SHEETS.stickers.columns,
            }),
        );
    }
    if (isPerishable) {
        layers.push(
            new Layer({
                pos: STICKER_MAP["Perishable"],
                name: "Perishable",
                order: 4,
                source: SPRITE_SHEETS.stickers.src,
                rows: SPRITE_SHEETS.stickers.rows,
                columns: SPRITE_SHEETS.stickers.columns,
            }),
        );
    }
    if (isRental) {
        layers.push(
            new Layer({
                pos: STICKER_MAP["Rental"],
                name: "Rental",
                order: 5,
                source: SPRITE_SHEETS.stickers.src,
                rows: SPRITE_SHEETS.stickers.rows,
                columns: SPRITE_SHEETS.stickers.columns,
            }),
        );
    }

    if (seal) {
        const sealPos = SEAL_MAP[seal];
        if (sealPos) {
            layers.push(
                new Layer({
                    pos: sealPos,
                    name: seal,
                    order: 6,
                    source: SPRITE_SHEETS.enhancers.src,
                    rows: SPRITE_SHEETS.enhancers.rows,
                    columns: SPRITE_SHEETS.enhancers.columns,
                }),
            );
        }
    }

    const wrapperStyle: React.CSSProperties = { width: `${71 * scale}px` };

    return (
        <div style={wrapperStyle} className={className}>
            <JamlCardRenderer invert={edition === "Negative"} layers={layers} hoverTilt={hoverTilt} />
        </div>
    );
}

export interface VoucherProps {
    voucherName: string;
    scale?: number;
    className?: string;
    hoverTilt?: boolean;
}

export function JamlVoucher({ voucherName, scale = 1, className = "", hoverTilt = false }: VoucherProps) {
    const voucherData = VOUCHERS.find((v) => v.name === voucherName);
    if (!voucherData) return null;

    const layers: Layer[] = [
        new Layer({
            ...voucherData,
            order: 0,
            source: SPRITE_SHEETS.vouchers.src,
            rows: SPRITE_SHEETS.vouchers.rows,
            columns: SPRITE_SHEETS.vouchers.columns,
        }),
    ];

    const wrapperStyle: React.CSSProperties = { width: `${71 * scale}px` };

    return (
        <div style={wrapperStyle} className={className}>
            <JamlCardRenderer layers={layers} hoverTilt={hoverTilt} />
        </div>
    );
}

export interface TagProps {
    tagName: string;
    scale?: number;
    className?: string;
    hoverTilt?: boolean;
}

export function JamlTag({ tagName, scale = 1, className = "", hoverTilt = false }: TagProps) {
    const tagData = TAGS.find((t) => t.name === tagName);
    if (!tagData) return null;

    const layers: Layer[] = [
        new Layer({
            ...tagData,
            order: 0,
            source: SPRITE_SHEETS.tags.src,
            rows: SPRITE_SHEETS.tags.rows,
            columns: SPRITE_SHEETS.tags.columns,
        }),
    ];

    const wrapperStyle: React.CSSProperties = { width: `${71 * scale}px` };

    return (
        <div style={wrapperStyle} className={className}>
            <JamlCardRenderer layers={layers} hoverTilt={hoverTilt} />
        </div>
    );
}

export interface BossProps {
    bossName: string;
    scale?: number;
    className?: string;
    hoverTilt?: boolean;
}

export function JamlBoss({ bossName, scale = 1, className = "", hoverTilt = false }: BossProps) {
    const bossData = BOSSES.find((b) => b.name === bossName);
    if (!bossData) return null;

    const layers: Layer[] = [
        new Layer({
            ...bossData,
            order: 0,
            source: SPRITE_SHEETS.blinds.src,
            rows: SPRITE_SHEETS.blinds.rows,
            columns: SPRITE_SHEETS.blinds.columns,
        }),
    ];

    const wrapperStyle: React.CSSProperties = { width: `${71 * scale}px` };

    return (
        <div style={wrapperStyle} className={className}>
            <JamlCardRenderer layers={layers} hoverTilt={hoverTilt} />
        </div>
    );
}
