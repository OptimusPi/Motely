"use client";

import React, { useState } from "react";
import { JimboText } from "../ui/jimboText.js";

export interface JamlSeedInputProps {
  value?: string;
  onChange?: (seed: string) => void;
  placeholder?: string;
  className?: string;
  style?: React.CSSProperties;
}

const SEED_PATTERN = /^[A-Z0-9]{0,8}$/;

/**
 * Balatro-styled seed input field.
 * Validates 1-8 uppercase alphanumeric characters.
 * All styling via jimbo.css `.j-seed-input` classes — zero inline styles.
 */
export function JamlSeedInput({ value, onChange, placeholder = "Enter seed (e.g. J4SPZMWW)", className, style }: JamlSeedInputProps) {
  const [internal, setInternal] = useState(value ?? "");
  const display = value ?? internal;
  const isValid = display.length === 0 || SEED_PATTERN.test(display);

  // Validation state drives data-valid attribute for CSS border color
  const validState: string =
    display.length === 0 ? "partial"
    : !isValid ? "false"
    : display.length === 8 ? "true"
    : "partial";

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 8);
    setInternal(raw);
    onChange?.(raw);
  };

  return (
    <div className={`j-seed-input ${className ?? ""}`} style={style}>
      <JimboText size="xs" tone="grey">Seed</JimboText>
      <input
        type="text"
        className="j-seed-input__field"
        data-valid={validState}
        value={display}
        onChange={handleChange}
        placeholder={placeholder}
        maxLength={8}
        spellCheck={false}
        autoComplete="off"
      />
      {display.length > 0 && display.length < 8 && (
        <span className="j-seed-input__hint">{8 - display.length} more characters</span>
      )}
    </div>
  );
}
