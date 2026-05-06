"use client";

import React, { useMemo } from "react";
import { JimboSprite } from "../ui/sprites.js";
import { JimboColorOption } from "../ui/tokens.js";
import type { SpriteSheetType } from "../sprites/spriteMapper.js";
import {
  extractVisualJamlItems,
  type JamlPreviewItem,
  type JamlPreviewSection,
  type JamlPreviewVisualType,
} from "../utils/jamlMapPreview.js";

const C = JimboColorOption;

export interface JamlMapPreviewProps {
  /** The raw JAML string to parse and visualize. */
  jaml: string;
  className?: string;
  emptyMessage?: string;
  tallyColumns?: number[];
  tallyLabels?: string[];
  /** Reduces padding and sizes for sidebar/explorer usage. */
  compact?: boolean;
}

const ZONES: Record<JamlPreviewSection, { label: string; color: string; glow: string }> = {
  must: { label: "Must", color: C.BLUE, glow: C.BLUE },
  should: { label: "Should", color: C.RED, glow: C.GOLD },
  mustNot: { label: "Must Not", color: C.ORANGE, glow: C.ORANGE },
};

const SECTION_ORDER: JamlPreviewSection[] = ["must", "should", "mustNot"];

const SHEET_FOR_VISUAL: Record<JamlPreviewVisualType, SpriteSheetType> = {
  joker: "Jokers",
  consumable: "Tarots",
  voucher: "Vouchers",
  tag: "tags",
  boss: "BlindChips",
};

/** 
 * Pulsing glow animation for hits.
 * Design ref: assets/...DesignsV2/src/v2/GlowRing.css
 */
const GLOW_ANIMATION = `
@keyframes j-glow-pulse {
  0% { box-shadow: 0 0 0 1px var(--glow-color), 0 0 4px var(--glow-color); opacity: 0.8; }
  50% { box-shadow: 0 0 0 2px var(--glow-color), 0 0 12px var(--glow-color); opacity: 1; }
  100% { box-shadow: 0 0 0 1px var(--glow-color), 0 0 4px var(--glow-color); opacity: 0.8; }
}
`;

function ClausePill({ 
  item, 
  glow, 
  matchCount 
}: { 
  item: JamlPreviewItem; 
  glow: string; 
  matchCount: number;
}) {
  const isHit = matchCount > 0;
  const hasData = matchCount !== undefined && matchCount >= 0;

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 6,
        background: isHit ? `${glow}33` : C.DARKEST,
        border: `2px solid ${isHit ? glow : C.PANEL_EDGE}`,
        borderRadius: 4,
        padding: "3px 8px",
        position: "relative",
        opacity: isHit ? 1 : 0.6,
        // eslint-disable-next-line @typescript-eslint/ban-ts-comment
        // @ts-ignore -- CSS custom property
        "--glow-color": glow,
        animation: isHit ? "j-glow-pulse 1.6s ease-in-out infinite" : "none",
      }}
      title={`${item.clauseKey}: ${item.value}${hasData ? ` (Found: ${matchCount})` : ""}`}
    >
      <style>{GLOW_ANIMATION}</style>
      <JimboSprite 
        name={item.value} 
        sheet={SHEET_FOR_VISUAL[item.visualType]} 
        width={26} 
      />
      <div
        style={{
          fontSize: 10,
          color: C.WHITE,
          letterSpacing: 0.5,
          textShadow: "1px 1px 0 rgba(0,0,0,.8)",
        }}
      >
        {item.value}
      </div>
      {isHit && (
        <div 
          style={{ 
            position: "absolute",
            top: -6,
            right: -6,
            background: C.GREEN,
            color: C.WHITE,
            fontSize: 7,
            padding: "1px 3px",
            borderRadius: 3,
            border: `1px solid ${C.BLACK}`,
            boxShadow: `0 1px 0 ${C.BLACK}`,
          }}
        >
          {matchCount > 1 ? `x${matchCount}` : "✓"}
        </div>
      )}
    </div>
  );
}


function ZoneRail({ 
  zone, 
  items, 
  matchMap,
  compact = false
}: { 
  zone: JamlPreviewSection; 
  items: JamlPreviewItem[];
  matchMap: Record<string, number>;
  compact?: boolean;
}) {
  const meta = ZONES[zone];
  return (
    <div
      style={{
        border: `2px dashed ${meta.color}55`,
        borderRadius: 6,
        padding: compact ? 4 : 8,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
        <div
          style={{
            fontSize: compact ? 8 : 10,
            letterSpacing: 2,
            padding: "2px 8px",
            background: meta.color,
            color: C.WHITE,
            borderRadius: 3,
            textShadow: "1px 1px 0 rgba(0,0,0,.8)",
          }}
        >
          {meta.label}
        </div>
        <div style={{ flex: 1, height: 1, background: `${meta.color}44` }} />
        <div style={{ fontSize: 8, color: C.GREY }}>{items.length}</div>
      </div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
        {items.length === 0 ? (
          <div style={{ fontSize: 10, color: C.GREY, padding: 10, fontStyle: "italic" }}>
            drop clauses here
          </div>
        ) : (
          items.map((item) => {
            // Match logic: the engine labels usually look like "must: joker: Blueprint"
            // or "must: rareJoker: Blueprint". We try to match the item value and section.
            const labelKey = `${item.section}: ${item.clauseKey}: ${item.value}`;
            const count = matchMap[labelKey] ?? -1;

            return (
              <ClausePill 
                key={item.id} 
                item={item} 
                glow={meta.glow}
                matchCount={count}
              />
            );
          })
        )}
      </div>
    </div>
  );
}

export function JamlMapPreview({
  jaml,
  className = "",
  emptyMessage = "No visual JAML clauses found yet.",
  tallyColumns,
  tallyLabels,
  compact = false,
}: JamlMapPreviewProps) {
  const groups = useMemo(() => extractVisualJamlItems(jaml), [jaml]);
  const totalItems = SECTION_ORDER.reduce((sum, s) => sum + groups[s].length, 0);

  const matchMap = useMemo(() => {
    const map: Record<string, number> = {};
    if (tallyColumns && tallyLabels) {
      tallyLabels.forEach((label, i) => {
        map[label] = tallyColumns[i] ?? 0;
      });
    }
    return map;
  }, [tallyColumns, tallyLabels]);

  if (totalItems === 0) {
    return (
      <div
        className={className}
        style={{
          background: C.DARKEST,
          border: `2px solid ${C.PANEL_EDGE}`,
          borderRadius: 6,
          padding: compact ? 8 : 16,
          color: C.GREY,
          fontSize: 11,
          fontStyle: "italic",
          textAlign: "center",
        }}
      >
        {emptyMessage}
      </div>
    );
  }

  return (
    <div
      className={className}
      style={{
        display: "flex",
        flexDirection: "column",
        gap: compact ? 6 : 10,
        padding: compact ? 6 : 10,
        background: C.DARKEST,
        color: C.WHITE,
      }}
    >
      {SECTION_ORDER.map((section) => (
        <ZoneRail 
          key={section} 
          zone={section} 
          items={groups[section]} 
          matchMap={matchMap}
          compact={compact}
        />
      ))}
    </div>
  );
}

