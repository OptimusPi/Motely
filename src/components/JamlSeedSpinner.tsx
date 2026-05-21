"use client";

import React, { useMemo, useState } from "react";
import { JimboBadge } from "../ui/JimboBadge.js";
import { JimboFlankNav } from "../ui/jimboFlankNav.js";
import { JimboSeedCopyChip } from "../ui/JimboSeedCopyChip.js";
import { JamlSeedInput, type JamlSeedInputProps } from "./JamlSeedInput.js";
import { normalizeJamlSeed } from "./jamlSeedUtils.js";

export interface JamlSeedSpinnerProps extends Omit<JamlSeedInputProps, "onChange"> {
  seeds?: string[];
  onChange?: (seed: string) => void;
  onCopy?: (seed: string) => void;
  /** When true, center field is editable. Default false — browse + copy chip. */
  editable?: boolean;
}

export function JamlSeedSpinner({
  seeds = [],
  value,
  onChange,
  onCopy,
  editable = false,
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
  const canSeek = normalizedSeeds.length >= 2;

  const setSeed = (nextSeed: string) => {
    const normalized = normalizeJamlSeed(nextSeed);
    if (value === undefined) {
      setInternal(normalized);
    }
    onChange?.(normalized);
  };

  const seek = (direction: -1 | 1) => {
    if (!canSeek) return;
    const baseIndex = activeIndex >= 0 ? activeIndex : 0;
    const nextIndex = (baseIndex + direction + normalizedSeeds.length) % normalizedSeeds.length;
    setSeed(normalizedSeeds[nextIndex]);
  };

  const rootClass = ["j-seed-spinner", className].filter(Boolean).join(" ");

  return (
    <div className={rootClass} style={style}>
      <div className="j-seed-spinner__meta">
        {label ? <span className="j-seed-spinner__label">{label}</span> : <span />}
        {normalizedSeeds.length > 0 ? (
          <JimboBadge size="sm" tone={variant === "dark" ? "grey" : "dark"}>
            {activeIndex >= 0 ? `${activeIndex + 1} of ${normalizedSeeds.length}` : `${normalizedSeeds.length} seeds`}
          </JimboBadge>
        ) : null}
      </div>

      <JimboFlankNav
        onPrev={() => seek(-1)}
        onNext={() => seek(1)}
        canPrev={canSeek}
        canNext={canSeek}
        prevLabel="Previous seed"
        nextLabel="Next seed"
        className="j-seed-spinner__nav"
      >
        {editable ? (
          <JamlSeedInput
            {...inputProps}
            value={display}
            onChange={setSeed}
            label={undefined}
            placeholder={placeholder}
            variant={variant}
          />
        ) : (
          <JimboSeedCopyChip
            value={display}
            placeholder={placeholder}
            onCopy={onCopy}
          />
        )}
      </JimboFlankNav>
    </div>
  );
}
