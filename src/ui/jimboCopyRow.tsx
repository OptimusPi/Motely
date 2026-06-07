"use client";

import React from "react";
import { JimboText } from "./jimboText.js";
import { JimboSeedCopyChip } from "./JimboSeedCopyChip.js";

export interface JimboCopyRowProps {
  value: string;
  label?: string;
}

/**
 * Label + WeeJoker-style seed copy chip. Use JimboSeedCopyChip directly when
 * no label is needed.
 */
export function JimboCopyRow({ value, label }: JimboCopyRowProps) {
  return (
    <div className="j-copy-row">
      {label ? (
        <JimboText size="xs" tone="grey" className="j-copy-row__label">
          {label}
        </JimboText>
      ) : null}
      <JimboSeedCopyChip value={value} />
    </div>
  );
}
