// PORTABLE — intended for jaml-ui/src/ui/jimboIconButton.tsx
// On paste, replace `from 'jaml-ui'` with `from './tokens.js'`.
"use client";

import { JimboColorOption } from "./tokens.js";
import type React from "react";
import { useState } from "react";

const C = JimboColorOption;

export interface JimboIconButtonProps {
  "aria-label"?: string;
  children: React.ReactNode;
  disabled?: boolean;
  onClick?: () => void;
  size?: "sm" | "md";
  title?: string;
}

export function JimboIconButton({
  onClick,
  title,
  "aria-label": ariaLabel,
  disabled = false,
  size = "md",
  children,
}: JimboIconButtonProps) {
  const [hover, setHover] = useState(false);
  const side = size === "sm" ? 26 : 30;

  return (
    <button
      aria-label={ariaLabel ?? title}
      disabled={disabled}
      onClick={() => !disabled && onClick?.()}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        width: side,
        height: side,
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        background: hover && !disabled ? C.DARK_GREY : C.DARKEST,
        color: C.WHITE,
        border: `1px solid ${C.PANEL_EDGE}`,
        borderRadius: 4,
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
