"use client";

import React, { useMemo, useState } from "react";
import { LuCopy } from "react-icons/lu";
import { JimboBadge } from "../ui/JimboBadge.js";
import { JimboButton } from "../ui/panel.js";
import { JamlSeedInput, type JamlSeedInputProps } from "./JamlSeedInput.js";
import { normalizeJamlSeed } from "./jamlSeedUtils.js";

export interface JamlSeedSpinnerProps extends Omit<JamlSeedInputProps, "onChange"> {
  seeds?: string[];
  onChange?: (seed: string) => void;
  onCopy?: (seed: string) => void;
}

export function JamlSeedSpinner({
  seeds = [],
  value,
  onChange,
  onCopy,
  label = "Seed",
  placeholder = "Aleeb",
  variant = "normal",
  className,
  style,
  ...inputProps
}: JamlSeedSpinnerProps) {
  const normalizedSeeds = useMemo(
    () => Array.from(new Set(seeds.map((seed) => normalizeJamlSeed(seed)).filter(Boolean))),
    [seeds],
  );
  const [internal, setInternal] = useState(() => normalizeJamlSeed(value ?? normalizedSeeds[0] ?? ""));
  const display = value === undefined ? internal : normalizeJamlSeed(value);
  const activeIndex = normalizedSeeds.indexOf(display);

  const setSeed = (nextSeed: string) => {
    const normalized = normalizeJamlSeed(nextSeed);
    if (value === undefined) {
      setInternal(normalized);
    }
    onChange?.(normalized);
  };

  const seek = (direction: -1 | 1) => {
    if (normalizedSeeds.length === 0) return;
    const baseIndex = activeIndex >= 0 ? activeIndex : 0;
    const nextIndex = (baseIndex + direction + normalizedSeeds.length) % normalizedSeeds.length;
    setSeed(normalizedSeeds[nextIndex]);
  };

  const handleCopy = async () => {
    if (!display) return;
    try {
      if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(display);
      }
    } catch {
      // Non-fatal in Storybook or restricted clipboard contexts.
    }
    onCopy?.(display);
  };

  return (
    <div className={`j-seed-spinner ${className ?? ""}`.trim()} style={style}>
      <div className="j-seed-spinner__meta">
        {label ? <span className="j-seed-spinner__label">{label}</span> : <span />}
        {normalizedSeeds.length > 0 ? (
          <JimboBadge size="sm" tone={variant === "dark" ? "grey" : "dark"}>
            {activeIndex >= 0 ? `${activeIndex + 1} of ${normalizedSeeds.length}` : `${normalizedSeeds.length} seeds`}
          </JimboBadge>
        ) : null}
      </div>
      <div className="j-seed-spinner__row">
        <JimboButton tone="red" size="sm" onClick={() => seek(-1)} disabled={normalizedSeeds.length < 2}>
          {"<"}
        </JimboButton>
        <JamlSeedInput
          {...inputProps}
          value={display}
          onChange={setSeed}
          label={undefined}
          placeholder={placeholder}
          variant={variant}
        />
        <JimboButton tone="grey" size="sm" onClick={handleCopy} disabled={!display}>
          <span className="j-seed-spinner__copy">
            <LuCopy size={12} aria-hidden />
            <span>Copy</span>
          </span>
        </JimboButton>
        <JimboButton tone="red" size="sm" onClick={() => seek(1)} disabled={normalizedSeeds.length < 2}>
          {">"}
        </JimboButton>
      </div>
    </div>
  );
}
