"use client";
import React, { useRef } from "react";
import { useJamlIdeDrag } from "../ui/hooks.js";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboSprite } from "../ui/sprites.js";
import type { SpriteSheetType } from "../sprites/spriteMapper.js";

export type JamlZone = "must" | "should" | "mustnot";

export interface JamlVisualClause {
    id: string;
    type: string;
    value: string;
    label?: string;
    antes?: number[];
    score?: number;
    edition?: string;
}

export interface JamlVisualFilter {
    name?: string;
    author?: string;
    description?: string;
    deck?: string;
    stake?: string;
    must: JamlVisualClause[];
    should: JamlVisualClause[];
    mustnot: JamlVisualClause[];
}

export interface JamlIdeVisualProps {
    filter: JamlVisualFilter;
    onChange: (filter: JamlVisualFilter) => void;
    /** Tap a clause to edit it (Pass 2 wires this to the cascade picker). */
    onEditClause?: (zone: JamlZone, clause: JamlVisualClause) => void;
    /** Tap the "?" mystery tile to add a new clause to a zone (Pass 2 wires this to the cascade picker). */
    onAddClause?: (zone: JamlZone) => void;
}

const C = JimboColorOption;

const ZONE_META: Record<JamlZone, { label: string; hint: string; color: string; accent: string }> = {
    must: { label: "Must", hint: "Seed must contain all of these.", color: C.BLUE, accent: "#4db5ff" },
    should: { label: "Should", hint: "Bonus points per match.", color: C.RED, accent: "#ff8076" },
    mustnot: { label: "Must Not", hint: "Rejected if any appear.", color: C.ORANGE, accent: "#ffb84d" },
};

function clauseSpriteSheet(type: string): SpriteSheetType | undefined {
    if (
        type === "joker" ||
        type === "souljoker" ||
        type === "rareJoker" ||
        type === "commonJoker" ||
        type === "uncommonJoker" ||
        type === "legendaryJoker" ||
        type === "mixedJoker"
    ) return "Jokers";
    if (type === "voucher") return "Vouchers";
    if (type === "smallblindtag" || type === "bigblindtag" || type === "tag") return "tags";
    if (type === "boss") return "BlindChips";
    if (type === "tarot" || type === "spectral") return "Tarots";
    return undefined;
}

function ClauseSprite({ clause, size = 40 }: { clause: JamlVisualClause; size?: number }) {
    const sheet = clauseSpriteSheet(clause.type);
    if (!sheet) return null;
    return <JimboSprite name={clause.value} sheet={sheet} width={size} />;
}

function ClauseCard({
    clause,
    zone,
    onRemove,
    onEdit,
    onDragStart,
}: {
    clause: JamlVisualClause;
    zone: JamlZone;
    onRemove: () => void;
    onEdit: () => void;
    onDragStart: (e: React.MouseEvent | React.TouchEvent, clause: JamlVisualClause, zone: JamlZone) => void;
}) {
    const z = ZONE_META[zone];
    return (
        <div
            onClick={onEdit}
            onMouseDown={(e) => onDragStart(e, clause, zone)}
            onTouchStart={(e) => onDragStart(e, clause, zone)}
            style={{
                position: "relative",
                background: C.DARK_GREY,
                border: `2px solid ${z.color}`,
                borderRadius: 6,
                padding: "8px 8px 8px 6px",
                display: "flex",
                alignItems: "center",
                gap: 8,
                minWidth: 0,
                cursor: "pointer",
                userSelect: "none",
                touchAction: "none",
                boxShadow: `0 2px 0 ${C.BLACK}`,
            }}
        >
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 44, height: 50, flexShrink: 0 }}>
                <ClauseSprite clause={clause} size={40} />
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
                <div
                    style={{
                        fontFamily: "m6x11plus, ui-monospace, monospace",
                        fontSize: 13,
                        color: C.WHITE,
                        letterSpacing: 0.5,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                    }}
                >
                    {clause.label || clause.value}
                </div>
                <div style={{ display: "flex", gap: 3, marginTop: 3, alignItems: "center", flexWrap: "wrap" }}>
                    {clause.antes && clause.antes.length > 0 && (
                        <>
                            <div style={{ fontFamily: "m6x11plus, ui-monospace, monospace", fontSize: 8, color: C.GREY, letterSpacing: 1 }}>A</div>
                            {clause.antes.map((a) => (
                                <div
                                    key={a}
                                    style={{
                                        fontFamily: "m6x11plus, ui-monospace, monospace",
                                        fontSize: 9,
                                        padding: "1px 4px",
                                        background: C.DARKEST,
                                        color: z.accent,
                                        borderRadius: 3,
                                        letterSpacing: 0.5,
                                        lineHeight: 1,
                                    }}
                                >
                                    {a}
                                </div>
                            ))}
                        </>
                    )}
                    {zone === "should" && clause.score != null && (
                        <div
                            style={{
                                marginLeft: 4,
                                fontFamily: "m6x11plus, ui-monospace, monospace",
                                fontSize: 9,
                                padding: "1px 5px",
                                background: C.RED,
                                color: C.WHITE,
                                borderRadius: 3,
                                letterSpacing: 0.5,
                                lineHeight: 1,
                            }}
                        >
                            +{clause.score}
                        </div>
                    )}
                </div>
            </div>
            <button
                onClick={(e) => {
                    e.stopPropagation();
                    onRemove();
                }}
                aria-label={`Remove ${clause.label || clause.value}`}
                style={{
                    width: 22,
                    height: 22,
                    flexShrink: 0,
                    border: `2px solid ${C.BLACK}`,
                    borderRadius: 4,
                    background: C.RED,
                    color: C.WHITE,
                    fontFamily: "m6x11plus, ui-monospace, monospace",
                    fontSize: 12,
                    lineHeight: 1,
                    cursor: "pointer",
                    padding: 0,
                    boxShadow: `inset 0 1px 0 rgba(255,255,255,.2), 0 2px 0 ${C.BLACK}`,
                }}
            >
                ×
            </button>
        </div>
    );
}

function MysteryAddTile({ zone, onTap }: { zone: JamlZone; onTap?: () => void }) {
    const z = ZONE_META[zone];
    return (
        <div
            onClick={onTap}
            style={{
                cursor: onTap ? "pointer" : "default",
                opacity: onTap ? 1 : 0.55,
                border: `2px dashed ${z.color}`,
                borderRadius: 6,
                padding: "12px 8px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                gap: 10,
                background: `${z.color}0d`,
                minHeight: 60,
            }}
        >
            <div
                style={{
                    width: 40,
                    height: 40,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    background: C.DARKEST,
                    border: `2px solid ${z.color}`,
                    borderRadius: 6,
                    fontFamily: "m6x11plus, ui-monospace, monospace",
                    fontSize: 24,
                    color: z.color,
                }}
            >
                ?
            </div>
            <div
                style={{
                    fontFamily: "m6x11plus, ui-monospace, monospace",
                    fontSize: 12,
                    color: z.accent,
                }}
            >
                Add to {z.label}
            </div>
        </div>
    );
}

function ZoneRail({
    zone,
    clauses,
    onAdd,
    onRemove,
    onEdit,
    onDragStart,
    highlight,
}: {
    zone: JamlZone;
    clauses: JamlVisualClause[];
    onAdd?: () => void;
    onRemove: (id: string) => void;
    onEdit: (clause: JamlVisualClause) => void;
    onDragStart: (e: React.MouseEvent | React.TouchEvent, clause: JamlVisualClause, zone: JamlZone) => void;
    highlight: boolean;
}) {
    const z = ZONE_META[zone];
    return (
        <div
            data-zone={zone}
            style={{
                border: `2px dashed ${highlight ? z.color : "transparent"}`,
                borderRadius: 6,
                padding: highlight ? 6 : 0,
                background: highlight ? `${z.color}11` : "transparent",
                transition: "background 100ms, border-color 100ms",
            }}
        >
            <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 6 }}>
                <div
                    style={{
                        fontFamily: "m6x11plus, ui-monospace, monospace",
                        fontSize: 12,
                        padding: "2px 6px",
                        background: z.color,
                        color: C.WHITE,
                        borderRadius: 3,
                        boxShadow: `0 2px 0 ${C.BLACK}`,
                    }}
                >
                    {z.label}
                </div>
                <div style={{ flex: 1, height: 2, background: `${z.color}55`, borderRadius: 1 }} />
                <div style={{ fontFamily: "m6x11plus, ui-monospace, monospace", fontSize: 8, color: C.GREY }}>
                    {clauses.length}
                </div>
            </div>
            <div style={{ fontFamily: "m6x11plus, ui-monospace, monospace", fontSize: 9, color: C.GREY, letterSpacing: 0.5, marginBottom: 8 }}>
                {z.hint}
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {clauses.map((c) => (
                    <ClauseCard
                        key={c.id}
                        clause={c}
                        zone={zone}
                        onRemove={() => onRemove(c.id)}
                        onEdit={() => onEdit(c)}
                        onDragStart={onDragStart}
                    />
                ))}
                <MysteryAddTile zone={zone} onTap={onAdd} />
            </div>
        </div>
    );
}

function TopMatter({
    filter,
    onChange,
}: {
    filter: JamlVisualFilter;
    onChange: (filter: JamlVisualFilter) => void;
}) {
    return (
        <div
            className="j-inner-panel"
            style={{ padding: 10 }}
        >
            <input
                value={filter.name ?? ""}
                placeholder="Untitled"
                onChange={(e) => onChange({ ...filter, name: e.target.value })}
                style={{
                    display: "block",
                    width: "100%",
                    background: "transparent",
                    border: "none",
                    outline: "none",
                    fontFamily: "m6x11plus, ui-monospace, monospace",
                    fontSize: 18,
                    color: C.WHITE,
                    letterSpacing: 1,
                    padding: 0,
                    marginBottom: 4,
                }}
            />
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                <div style={{ fontFamily: "m6x11plus, ui-monospace, monospace", fontSize: 10, color: C.WHITE }}>By</div>
                <input
                    value={filter.author ?? ""}
                    placeholder="anonymous"
                    onChange={(e) => onChange({ ...filter, author: e.target.value })}
                    style={{
                        flex: 1,
                        background: "transparent",
                        border: "none",
                        outline: "none",
                        fontFamily: "m6x11plus, ui-monospace, monospace",
                        fontSize: 12,
                        color: C.GOLD_TEXT,
                        padding: 0,
                    }}
                />
            </div>
            <input
                value={filter.description ?? ""}
                placeholder="description"
                onChange={(e) => onChange({ ...filter, description: e.target.value })}
                style={{
                    display: "block",
                    width: "100%",
                    marginTop: 6,
                    background: "transparent",
                    border: "none",
                    outline: "none",
                    fontFamily: "m6x11plus, ui-monospace, monospace",
                    fontSize: 11,
                    color: C.WHITE,
                    opacity: 0.8,
                    lineHeight: 1.35,
                    padding: 0,
                }}
            />
        </div>
    );
}

interface DragState {
    clause: JamlVisualClause;
    fromZone: JamlZone;
    x: number;
    y: number;
    offX: number;
    offY: number;
}

export function JamlIdeVisual({ filter, onChange, onEditClause, onAddClause }: JamlIdeVisualProps) {
    const rootRef = useRef<HTMLDivElement>(null);
    const { drag, hoverZone, onDragStart } = useJamlIdeDrag(filter, onChange, rootRef);

    const removeClause = (zone: JamlZone, id: string) => {
        onChange({ ...filter, [zone]: filter[zone].filter((c) => c.id !== id) });
    };

    return (
        <div
            ref={rootRef}
            style={{
                display: "flex",
                flexDirection: "column",
                gap: 14,
                padding: 10,
                background: C.DARKEST,
                color: C.WHITE,
            }}
        >
            <TopMatter filter={filter} onChange={onChange} />

            <div style={{ display: "flex", gap: 10 }}>
                <div style={{ flex: 1, minWidth: 0 }}>
                    <ZoneRail
                        zone="must"
                        clauses={filter.must}
                        onAdd={onAddClause ? () => onAddClause("must") : undefined}
                        onRemove={(id) => removeClause("must", id)}
                        onEdit={(c) => onEditClause?.("must", c)}
                        onDragStart={onDragStart}
                        highlight={hoverZone === "must"}
                    />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                    <ZoneRail
                        zone="mustnot"
                        clauses={filter.mustnot}
                        onAdd={onAddClause ? () => onAddClause("mustnot") : undefined}
                        onRemove={(id) => removeClause("mustnot", id)}
                        onEdit={(c) => onEditClause?.("mustnot", c)}
                        onDragStart={onDragStart}
                        highlight={hoverZone === "mustnot"}
                    />
                </div>
            </div>
            <ZoneRail
                zone="should"
                clauses={filter.should}
                onAdd={onAddClause ? () => onAddClause("should") : undefined}
                onRemove={(id) => removeClause("should", id)}
                onEdit={(c) => onEditClause?.("should", c)}
                onDragStart={onDragStart}
                highlight={hoverZone === "should"}
            />

            {drag && (
                <div
                    style={{
                        position: "fixed",
                        left: drag.x - drag.offX,
                        top: drag.y - drag.offY,
                        pointerEvents: "none",
                        zIndex: 999,
                        transform: "rotate(-2deg) scale(1.05)",
                        filter: `drop-shadow(0 4px 6px ${C.BLACK}99)`,
                        opacity: 0.92,
                    }}
                >
                    <ClauseCard
                        clause={drag.clause}
                        zone={drag.fromZone}
                        onRemove={() => {}}
                        onEdit={() => {}}
                        onDragStart={() => {}}
                    />
                </div>
            )}
        </div>
    );
}
