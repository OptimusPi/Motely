"use client";
import React, { useState } from "react";
import { JimboSprite } from "../../ui/sprites.js";
import { JimboColorOption, withAlpha, JIMBO_ANIMATIONS } from "../../ui/tokens.js";
import type { SpriteSheetType } from "../../sprites/spriteMapper.js";
import { WILDCARD_SPRITES } from "../../sprites/spriteMapper.js";

// ─── Types ───────────────────────────────────────────────────────────────────

export type JamlZone = "must" | "should" | "mustnot";

/** The item category that can appear in a slot. */
export type SlotCategory =
  | "joker" | "voucher" | "tag" | "boss"
  | "tarot" | "spectral" | "planet" | "pack";

/** Result of selecting a specific item in a slot. */
export interface SlotSelection {
  category: SlotCategory;
  /** The specific item name, or "Any" for wildcards. */
  value: string;
  /** JAML clause key (e.g. "commonJoker", "legendaryJoker", "voucher"). */
  clauseKey: string;
  /** Optional rarity for jokers. */
  rarity?: "common" | "uncommon" | "rare" | "legendary";
}

export interface MysterySlotProps {
  /** Which zone this slot belongs to — determines border color. */
  zone: JamlZone;
  /** What sheet type to render mystery card from. */
  sheetType: SpriteSheetType;
  /** Current selection, if any. */
  selection?: SlotSelection;
  /** Width of the card sprite. */
  width?: number;
  /** Called when the slot is tapped (to open a picker). */
  onTap?: () => void;
  /** Called when the selection is cleared. */
  onClear?: () => void;
  /** Additional inline styles. */
  style?: React.CSSProperties;
}

// ─── Zone colors ─────────────────────────────────────────────────────────────

const C = JimboColorOption;

const ZONE_BORDER: Record<JamlZone, string> = {
  must:    C.BLUE,
  should:  C.RED,
  mustnot: C.ORANGE,
};

// ─── Sheet → "Any" wildcard mapping ─────────────────────────────────────────

function getWildcardName(category?: SlotCategory): string | null {
  if (!category) return null;
  switch (category) {
    case "joker":    return null; // uses generic mystery
    case "voucher":  return null;
    case "tag":      return null;
    case "boss":     return null;
    case "tarot":    return null;
    case "spectral": return null;
    case "planet":   return null;
    case "pack":     return null;
    default:         return null;
  }
}

// ─── Component ───────────────────────────────────────────────────────────────

export function MysterySlot({
  zone,
  sheetType,
  selection,
  width = 56,
  onTap,
  onClear,
  style,
}: MysterySlotProps) {
  const [hover, setHover] = useState(false);
  const [pressed, setPressed] = useState(false);
  const [tilt, setTilt] = useState({ rx: 0, ry: 0, tx: 0, ty: 0 });
  const cardRef = React.useRef<HTMLDivElement>(null);

  const borderColor = ZONE_BORDER[zone];
  const isEmpty = !selection;
  const cardH = Math.round((width * 95) / 71);

  // Determine what to render
  const spriteName = selection?.value ?? "";
  const spriteSheet = selection ? categoryToSheet(selection.category) ?? sheetType : sheetType;

  const scale = pressed
    ? 0.95
    : hover
    ? JIMBO_ANIMATIONS.JUICE_UP_SCALE
    : 1;

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!cardRef.current) return;
    const rect = cardRef.current.getBoundingClientRect();
    // Normalize coordinates: -1 to 1
    const nx = Math.max(-1, Math.min(1, ((e.clientX - rect.left) / rect.width - 0.5) * 2));
    const ny = Math.max(-1, Math.min(1, ((e.clientY - rect.top) / rect.height - 0.5) * 2));

    setTilt({
      rx: ny * -20, // max 20deg tilt
      ry: nx * 20,
      tx: nx * -4,  // subtle shift
      ty: ny * -4,
    });
  };

  const handleMouseLeave = () => {
    setHover(false);
    setPressed(false);
    setTilt({ rx: 0, ry: 0, tx: 0, ty: 0 });
  };

  return (
    <div
      ref={cardRef}
      onClick={onTap}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={handleMouseLeave}
      onMouseMove={handleMouseMove}
      onMouseDown={() => setPressed(true)}
      onMouseUp={() => setPressed(false)}
      style={{
        position: "relative",
        width: width + 8,
        height: cardH + 8,
        cursor: onTap ? "pointer" : "default",
        ...style,
      }}
    >
      <div
        style={{
          position: "absolute",
          inset: 0,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          borderRadius: 6,
          border: isEmpty
            ? `2px dashed ${withAlpha(borderColor, 0.4)}`
            : `3px solid ${borderColor}`,
          background: isEmpty
            ? withAlpha(borderColor, 0.06)
            : withAlpha(C.DARKEST, 0.8),
          transform: `perspective(600px) scale(${scale}) rotateX(${tilt.rx}deg) rotateY(${tilt.ry}deg) translate(${tilt.tx}px, ${tilt.ty}px)`,
          transformStyle: "preserve-3d",
          transition: hover
            ? `border-color 200ms`
            : `transform 400ms ${JIMBO_ANIMATIONS.JUICE_EASING}, border-color 200ms`,
          boxShadow: hover ? `0 8px 16px ${withAlpha(C.BLACK, 0.4)}` : `0 2px 4px ${withAlpha(C.BLACK, 0.2)}`,
          zIndex: hover ? 10 : 1,
          pointerEvents: "none",
        }}
      >
      {/* The sprite */}
      <JimboSprite
        name={spriteName}
        sheet={spriteSheet}
        width={width}
        style={{
          opacity: isEmpty ? 0.5 : 1,
          filter: isEmpty ? "saturate(0.3)" : "none",
          transition: "opacity 200ms, filter 200ms",
        }}
      />

      {/* Clear button (×) when selected */}
      {selection && onClear && (
        <div
          onClick={(e) => { e.stopPropagation(); onClear(); }}
          style={{
            position: "absolute",
            top: -6,
            right: -6,
            width: 18,
            height: 18,
            borderRadius: "50%",
            background: C.RED,
            color: C.WHITE,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: 11,
            fontFamily: "m6x11plus, ui-monospace, monospace",
            cursor: "pointer",
            lineHeight: 1,
            boxShadow: `0 1px 4px ${withAlpha(C.BLACK, 0.5)}`,
            transform: "translateZ(10px)", // Pop out in 3D
          }}
        >
          ×
        </div>
      )}

      {/* Zone label on hover for empty slots */}
      {isEmpty && hover && (
        <div
          style={{
            position: "absolute",
            bottom: -16,
            left: "50%",
            transform: "translateX(-50%) translateZ(10px)",
            fontFamily: "m6x11plus, ui-monospace, monospace",
            fontSize: 10,
            color: borderColor,
            whiteSpace: "nowrap",
            textTransform: "uppercase",
            letterSpacing: 1,
          }}
        >
          + tap
        </div>
      )}
      </div>
    </div>
  );
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function categoryToSheet(cat: SlotCategory): SpriteSheetType | null {
  switch (cat) {
    case "joker":    return "Jokers";
    case "voucher":  return "Vouchers";
    case "tag":      return "tags";
    case "boss":     return "BlindChips";
    case "tarot":
    case "spectral":
    case "planet":   return "Tarots";
    case "pack":     return "Boosters";
    default:         return null;
  }
}
