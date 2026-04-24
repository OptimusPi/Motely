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
  jaml: string;
  className?: string;
  emptyMessage?: string;
}

const ZONES: Record<JamlPreviewSection, { label: string; color: string }> = {
  must: { label: "MUST", color: C.BLUE },
  should: { label: "SHOULD", color: C.RED },
  mustNot: { label: "MUST NOT", color: C.ORANGE },
};

const SECTION_ORDER: JamlPreviewSection[] = ["must", "should", "mustNot"];

const SHEET_FOR_VISUAL: Record<JamlPreviewVisualType, SpriteSheetType> = {
  joker: "Jokers",
  consumable: "Tarots",
  voucher: "Vouchers",
  tag: "tags",
  boss: "BlindChips",
};

function ClausePill({ item, color }: { item: JamlPreviewItem; color: string }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 6,
        background: C.DARK_GREY,
        border: `2px solid ${color}`,
        borderRadius: 6,
        padding: "5px 8px 5px 4px",
        boxShadow: `0 2px 0 ${C.BLACK}`,
      }}
      title={`${item.clauseKey}: ${item.value}`}
    >
      <div style={{ color: C.GREY, fontSize: 12, lineHeight: 1, padding: "0 2px" }}>⋮⋮</div>
      <JimboSprite name={item.value} sheet={SHEET_FOR_VISUAL[item.visualType]} width={26} />
      <div
        style={{
          fontSize: 10,
          color: C.WHITE,
          letterSpacing: 1,
          textShadow: "1px 1px 0 rgba(0,0,0,.8)",
        }}
      >
        {item.value}
      </div>
    </div>
  );
}

function ZoneRail({ zone, items }: { zone: JamlPreviewSection; items: JamlPreviewItem[] }) {
  const meta = ZONES[zone];
  return (
    <div
      style={{
        border: `2px dashed ${meta.color}55`,
        borderRadius: 6,
        padding: 8,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
        <div
          style={{
            fontSize: 10,
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
          items.map((item) => <ClausePill key={item.id} item={item} color={meta.color} />)
        )}
      </div>
    </div>
  );
}

export function JamlMapPreview({
  jaml,
  className = "",
  emptyMessage = "No visual JAML clauses found yet.",
}: JamlMapPreviewProps) {
  const groups = useMemo(() => extractVisualJamlItems(jaml), [jaml]);
  const totalItems = SECTION_ORDER.reduce((sum, s) => sum + groups[s].length, 0);

  if (totalItems === 0) {
    return (
      <div
        className={className}
        style={{
          background: C.DARKEST,
          border: `2px solid ${C.PANEL_EDGE}`,
          borderRadius: 6,
          padding: 16,
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
        gap: 10,
        padding: 10,
        background: C.DARKEST,
        color: C.WHITE,
      }}
    >
      {SECTION_ORDER.map((section) => (
        <ZoneRail key={section} zone={section} items={groups[section]} />
      ))}
    </div>
  );
}
