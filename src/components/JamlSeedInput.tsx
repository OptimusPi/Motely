"use client";

import React, { useState } from "react";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboText } from "../ui/jimboText.js";

export interface JamlSeedInputProps {
  value?: string;
  onChange?: (seed: string) => void;
  placeholder?: string;
  className?: string;
  style?: React.CSSProperties;
}

const SEED_PATTERN = /^[A-Z0-9]{0,8}$/;

export function JamlSeedInput({ value, onChange, placeholder = "Enter seed (e.g. J4SPZMWW)", className, style }: JamlSeedInputProps) {
  const [internal, setInternal] = useState(value ?? "");
  const display = value ?? internal;
  const isValid = display.length === 0 || SEED_PATTERN.test(display);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 8);
    setInternal(raw);
    onChange?.(raw);
  };

  return (
    <div className={className} style={{ display: "flex", flexDirection: "column", gap: 4, ...style }}>
      <JimboText size="xs" tone="grey">Seed</JimboText>
      <input
        type="text"
        value={display}
        onChange={handleChange}
        placeholder={placeholder}
        maxLength={8}
        spellCheck={false}
        autoComplete="off"
        style={{
          padding: "6px 10px",
          borderRadius: 6,
          border: `2px solid ${!isValid ? JimboColorOption.RED : display.length === 8 ? JimboColorOption.GREEN : JimboColorOption.PANEL_EDGE}`,
          background: JimboColorOption.DARKEST,
          color: JimboColorOption.GOLD_TEXT,
          fontSize: 16,
          fontFamily: "m6x11plus, monospace",
          letterSpacing: 2,
          textTransform: "uppercase",
          outline: "none",
          transition: "border-color 100ms",
        }}
      />
      {display.length > 0 && display.length < 8 && (
        <JimboText size="xs" tone="grey">{8 - display.length} more characters</JimboText>
      )}
    </div>
  );
}
