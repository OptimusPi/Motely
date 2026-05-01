// PORTABLE — intended for jaml-ui/src/ui/jimboSelect.tsx
// On paste, replace `from 'jaml-ui'` with `from './tokens.js'`.
"use client";

import { JimboColorOption } from "./tokens.js";
import type React from "react";

const C = JimboColorOption;

export interface JimboSelectOption {
  disabled?: boolean;
  label?: string;
  value: string;
}

export interface JimboSelectProps {
  "aria-label"?: string;
  disabled?: boolean;
  fullWidth?: boolean;
  onChange: (value: string) => void;
  options: JimboSelectOption[] | string[];
  placeholder?: string;
  size?: "sm" | "md";
  style?: React.CSSProperties;
  value: string;
}

export function JimboSelect({
  value,
  options,
  onChange,
  placeholder,
  disabled = false,
  fullWidth = true,
  size = "md",
  style,
  "aria-label": ariaLabel,
}: JimboSelectProps) {
  let normalized: JimboSelectOption[];
  if (options.length === 0) {
    normalized = [];
  } else if (typeof options[0] === "string") {
    normalized = (options as string[]).map((v) => ({ value: v }));
  } else {
    normalized = options as JimboSelectOption[];
  }

  const pad = size === "sm" ? "4px 8px" : "6px 10px";
  const fontSize = size === "sm" ? 11 : 12;

  return (
    <select
      aria-label={ariaLabel}
      disabled={disabled}
      onChange={(e) => onChange(e.target.value)}
      style={{
        width: fullWidth ? "100%" : undefined,
        padding: pad,
        background: C.DARKEST,
        color: C.WHITE,
        border: `1px solid ${C.PANEL_EDGE}`,
        borderRadius: 4,
        fontSize,
        fontFamily: "m6x11plus, monospace",
        cursor: disabled ? "not-allowed" : "pointer",
        opacity: disabled ? 0.55 : 1,
        appearance: "none",
        backgroundImage:
          "linear-gradient(45deg, transparent 50%, " +
          C.GOLD_TEXT +
          " 50%), linear-gradient(135deg, " +
          C.GOLD_TEXT +
          " 50%, transparent 50%)",
        backgroundPosition: "calc(100% - 12px) 50%, calc(100% - 7px) 50%",
        backgroundSize: "5px 5px, 5px 5px",
        backgroundRepeat: "no-repeat",
        paddingRight: 22,
        ...style,
      }}
      value={value}
    >
      {placeholder !== undefined && (
        <option disabled value="">
          {placeholder}
        </option>
      )}
      {normalized.map((opt) => (
        <option disabled={opt.disabled} key={opt.value} value={opt.value}>
          {opt.label ?? opt.value}
        </option>
      ))}
    </select>
  );
}
