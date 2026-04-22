"use client";

import React, { useEffect, useRef, useState } from "react";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboSprite } from "../ui/sprites.js";

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
  onSave?: () => void;
  onBack?: () => void;
}

const ZONE_META: Record<JamlZone, { label: string; color: string }> = {
  must:    { label: "MUST",     color: JimboColorOption.BLUE   },
  should:  { label: "SHOULD",   color: JimboColorOption.RED    },
  mustnot: { label: "MUST NOT", color: JimboColorOption.ORANGE },
};

function clauseSpriteSheet(type: string): "Jokers" | "Vouchers" | "tags" | "BlindChips" | "Tarots" | undefined {
  if (type === "joker" || type === "souljoker" || type === "rareJoker" || type === "commonJoker" || type === "uncommonJoker" || type === "legendaryJoker" || type === "mixedJoker") return "Jokers";
  if (type === "voucher") return "Vouchers";
  if (type === "smallblindtag" || type === "bigblindtag" || type === "tag") return "tags";
  if (type === "boss") return "BlindChips";
  if (type === "tarot" || type === "spectral") return "Tarots";
  return undefined;
}

function ClauseSprite({ clause, size = 26 }: { clause: JamlVisualClause; size?: number }) {
  const sheet = clauseSpriteSheet(clause.type);
  if (!sheet) return null;
  return <JimboSprite name={clause.value} sheet={sheet} width={size} />;
}

function DragClausePill({
  clause,
  zone,
  onDragStart,
}: {
  clause: JamlVisualClause;
  zone: JamlZone;
  onDragStart: (e: React.MouseEvent | React.TouchEvent, clause: JamlVisualClause, zone: JamlZone) => void;
}) {
  const z = ZONE_META[zone];
  return (
    <div
      onMouseDown={(e) => onDragStart(e, clause, zone)}
      onTouchStart={(e) => onDragStart(e, clause, zone)}
      style={{
        display: "flex", alignItems: "center", gap: 6,
        background: JimboColorOption.DARK_GREY, border: `2px solid ${z.color}`,
        borderRadius: 6, padding: "5px 8px 5px 4px",
        boxShadow: `0 2px 0 ${JimboColorOption.BLACK}`,
        cursor: "grab", userSelect: "none", touchAction: "none",
      }}
    >
      <div style={{ color: JimboColorOption.GREY, fontSize: 12, lineHeight: 1, padding: "0 2px" }}>⋮⋮</div>
      <ClauseSprite clause={clause} size={26} />
      <div style={{ fontSize: 10, color: JimboColorOption.WHITE, letterSpacing: 1, textShadow: `1px 1px 0 ${JimboColorOption.BLACK}cc` }}>
        {clause.label || clause.value}
      </div>
      {clause.antes && clause.antes.length > 0 && (
        <div style={{ display: "flex", gap: 2 }}>
          {clause.antes.slice(0, 3).map((a) => (
            <div key={a} style={{ fontSize: 8, padding: "0 3px", background: JimboColorOption.DARKEST, color: z.color, borderRadius: 2 }}>{a}</div>
          ))}
          {clause.antes.length > 3 && <div style={{ fontSize: 8, color: JimboColorOption.GREY }}>+{clause.antes.length - 3}</div>}
        </div>
      )}
      {clause.score != null && (
        <div style={{ fontSize: 9, padding: "0 4px", background: JimboColorOption.RED, color: JimboColorOption.WHITE, borderRadius: 2 }}>
          +{clause.score}
        </div>
      )}
    </div>
  );
}

function ZoneDropRail({
  zone,
  clauses,
  onDragStart,
  highlight,
}: {
  zone: JamlZone;
  clauses: JamlVisualClause[];
  onDragStart: (e: React.MouseEvent | React.TouchEvent, clause: JamlVisualClause, zone: JamlZone) => void;
  highlight: boolean;
}) {
  const z = ZONE_META[zone];
  return (
    <div
      data-zone={zone}
      style={{
        border: `2px dashed ${highlight ? z.color : `${z.color}55`}`,
        background: highlight ? `${z.color}22` : "transparent",
        borderRadius: 6, padding: 8,
        transition: "background 100ms, border-color 100ms",
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
        <div style={{
          fontSize: 10, letterSpacing: 2, padding: "2px 8px",
          background: z.color, color: JimboColorOption.WHITE, borderRadius: 3,
          textShadow: `1px 1px 0 ${JimboColorOption.BLACK}cc`,
        }}>{z.label}</div>
        <div style={{ flex: 1, height: 1, background: `${z.color}44` }} />
        <div style={{ fontSize: 8, color: JimboColorOption.GREY }}>{clauses.length}</div>
      </div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
        {clauses.map((c) => (
          <DragClausePill key={c.id} clause={c} zone={zone} onDragStart={onDragStart} />
        ))}
        {clauses.length === 0 && (
          <div style={{ fontSize: 10, color: JimboColorOption.GREY, padding: 10, fontStyle: "italic" }}>drop clauses here</div>
        )}
      </div>
    </div>
  );
}

interface DragState {
  clause: JamlVisualClause;
  fromZone: JamlZone;
  x: number; y: number;
  offX: number; offY: number;
  w: number; h: number;
}

export function JamlIdeVisual({ filter, onChange, onSave, onBack }: JamlIdeVisualProps) {
  const [drag, setDrag] = useState<DragState | null>(null);
  const [hoverZone, setHoverZone] = useState<string | null>(null);
  const rootRef = useRef<HTMLDivElement>(null);

  const onDragStart = (
    e: React.MouseEvent | React.TouchEvent,
    clause: JamlVisualClause,
    fromZone: JamlZone,
  ) => {
    e.preventDefault();
    const t = "touches" in e ? e.touches[0] : e;
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    setDrag({ clause, fromZone, x: t.clientX, y: t.clientY, offX: t.clientX - rect.left, offY: t.clientY - rect.top, w: rect.width, h: rect.height });
  };

  useEffect(() => {
    if (!drag) return;
    const move = (e: MouseEvent | TouchEvent) => {
      const t = "touches" in e ? (e as TouchEvent).touches[0] : (e as MouseEvent);
      setDrag((d) => d && { ...d, x: t.clientX, y: t.clientY });
      const rails = rootRef.current?.querySelectorAll("[data-zone]") ?? [];
      let found: string | null = null;
      for (const r of rails) {
        const rc = r.getBoundingClientRect();
        if (t.clientX >= rc.left && t.clientX <= rc.right && t.clientY >= rc.top && t.clientY <= rc.bottom) {
          found = r.getAttribute("data-zone"); break;
        }
      }
      setHoverZone(found);
    };
    const up = () => {
      if (hoverZone && hoverZone !== drag.fromZone) {
        const from = hoverZone as JamlZone;
        onChange({
          ...filter,
          [drag.fromZone]: filter[drag.fromZone].filter((c) => c.id !== drag.clause.id),
          [from]: [...filter[from], { ...drag.clause }],
        });
      }
      setDrag(null);
      setHoverZone(null);
    };
    window.addEventListener("mousemove", move);
    window.addEventListener("mouseup", up);
    window.addEventListener("touchmove", move, { passive: false });
    window.addEventListener("touchend", up);
    return () => {
      window.removeEventListener("mousemove", move);
      window.removeEventListener("mouseup", up);
      window.removeEventListener("touchmove", move);
      window.removeEventListener("touchend", up);
    };
  }, [drag, hoverZone, filter, onChange]);

  return (
    <div ref={rootRef} style={{ display: "flex", flexDirection: "column", gap: 10, padding: 10 }}>
      {/* File info */}
      <div style={{
        background: JimboColorOption.DARK_GREY, border: `2px solid ${JimboColorOption.PANEL_EDGE}`,
        borderRadius: 6, padding: 8, boxShadow: `0 2px 0 ${JimboColorOption.BLACK}`,
      }}>
        <div style={{ fontSize: 9, color: JimboColorOption.GREY, letterSpacing: 2 }}>FILE</div>
        <div style={{ fontSize: 14, color: JimboColorOption.WHITE, textShadow: `1px 1px 0 ${JimboColorOption.BLACK}cc` }}>
          {filter.name || "Untitled"}.jaml
        </div>
        {filter.author && (
          <div style={{ fontSize: 9, color: JimboColorOption.GOLD_TEXT, marginTop: 2 }}>by {filter.author}</div>
        )}
      </div>

      <ZoneDropRail zone="must"    clauses={filter.must}    onDragStart={onDragStart} highlight={hoverZone === "must"} />
      <ZoneDropRail zone="should"  clauses={filter.should}  onDragStart={onDragStart} highlight={hoverZone === "should"} />
      <ZoneDropRail zone="mustnot" clauses={filter.mustnot} onDragStart={onDragStart} highlight={hoverZone === "mustnot"} />

      {/* Drag ghost */}
      {drag && (
        <div style={{
          position: "fixed",
          left: drag.x - drag.offX, top: drag.y - drag.offY,
          pointerEvents: "none", zIndex: 999,
          transform: "rotate(-2deg) scale(1.05)",
          filter: `drop-shadow(0 4px 6px ${JimboColorOption.BLACK}99)`, opacity: 0.92,
        }}>
          <DragClausePill clause={drag.clause} zone={drag.fromZone} onDragStart={() => {}} />
        </div>
      )}
    </div>
  );
}
