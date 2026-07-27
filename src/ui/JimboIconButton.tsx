// PORTABLE — intended for jaml-ui/src/ui/jimboIconButton.tsx
// On paste, replace `from 'jaml-ui'` with `from './tokens.js'`.
"use client";

import { JimboColorOption } from "./tokens.js";
import type React from "react";
import { useState } from "react";

const C = JimboColorOption;

export type JimboIconButtonTone = "default" | "destructive";

export interface JimboIconButtonProps {
  "aria-label"?: string;
  children: React.ReactNode;
  disabled?: boolean;
  onClick?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  /** Pass-through for drag-start guards (e.g. stopPropagation inside draggable rows). */
  onMouseDown?: (e: React.MouseEvent<HTMLButtonElement>) => void;
  onTouchStart?: (e: React.TouchEvent<HTMLButtonElement>) => void;
  size?: "xs" | "sm" | "md";
  tone?: JimboIconButtonTone;
  title?: string;
}

const SIZE_PX: Record<NonNullable<JimboIconButtonProps["size"]>, number> = {
  xs: 22,
  sm: 26,
  md: 30,
};

const TONE_BG: Record<JimboIconButtonTone, { rest: string; hover: string; border: string }> = {
  default:     { rest: C.DARKEST, hover: C.DARK_GREY, border: C.PANEL_EDGE },
  destructive: { rest: C.RED,     hover: C.ORANGE,    border: C.BLACK },
};

export function JimboIconButton({
  onClick,
  onMouseDown,
  onTouchStart,
  title,
  "aria-label": ariaLabel,
  disabled = false,
  size = "md",
  tone = "default",
  children,
}: JimboIconButtonProps) {
  const [hover, setHover] = useState(false);
  const side = SIZE_PX[size];
  const palette = TONE_BG[tone];
  // Destructive uses the Balatro "raised chip" look (thicker dark border +
  // bottom drop-shadow) to read clearly against red/orange clause backgrounds.
  const borderWidth = tone === "destructive" ? 2 : 1;
  const boxShadow =
    tone === "destructive"
      ? `inset 0 1px 0 rgba(255,255,255,.2), 0 2px 0 ${C.BLACK}`
      : undefined;

  return (
    <button
      aria-label={ariaLabel ?? title}
      disabled={disabled}
      onClick={(e) => !disabled && onClick?.(e)}
      onMouseDown={onMouseDown}
      onTouchStart={onTouchStart}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        width: side,
        height: side,
        display: "inline-grid",
        placeItems: "center",
        background: hover && !disabled ? palette.hover : palette.rest,
        color: C.WHITE,
        border: `${borderWidth}px solid ${palette.border}`,
        borderRadius: 4,
        boxShadow,
        cursor: disabled ? "not-allowed" : "pointer",
        opacity: disabled ? 0.55 : 1,
        padding: 0,
        fontSize: 14,
        lineHeight: 1,
        transition: "background 80ms ease",
      }}
      title={title}
      type="button"
    >
      {children}
    </button>
  );
}
