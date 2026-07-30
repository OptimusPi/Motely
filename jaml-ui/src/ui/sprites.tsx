"use client";

import type { CSSProperties } from "react";
import { resolveJamlAssetUrl } from "../assets.js";
import { SHEET_META, getSpriteDataOrMystery, type SpriteSheetType } from "../sprites/spriteMapper.js";

export interface JimboSpriteProps {
  name: string;
  sheet: SpriteSheetType;
  width: number;
  className?: string;
  style?: CSSProperties;
}

// Card-shaped sheets (jokers/tarots/vouchers/packs) render taller than wide,
// matching the standard Balatro card proportion; chip/icon sheets are square.
const CARD_SHEETS = new Set<SpriteSheetType>(["Jokers", "Tarots", "Vouchers", "Boosters"]);

export function JimboSprite({ name, sheet, width, className, style }: JimboSpriteProps) {
  const { pos, type } = getSpriteDataOrMystery(name, sheet);
  const meta = SHEET_META[type];
  const height = CARD_SHEETS.has(type) ? Math.round(width * (95 / 71)) : width;

  return (
    <div
      className={className}
      style={{
        position: "relative",
        display: "inline-block",
        width,
        height,
        overflow: "hidden",
        imageRendering: "pixelated",
        ...style,
      }}
      title={name}
    >
      <div
        style={{
          position: "absolute",
          inset: 0,
          backgroundImage: `url(${resolveJamlAssetUrl(meta.assetKey)})`,
          backgroundSize: `${meta.cols * 100}% ${meta.rows * 100}%`,
          backgroundPosition: `${(pos.x / (meta.cols - 1)) * 100}% ${(pos.y / (meta.rows - 1)) * 100}%`,
          backgroundRepeat: "no-repeat",
        }}
      />
    </div>
  );
}
