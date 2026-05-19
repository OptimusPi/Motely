"use client";

import React from "react";
import { JimboSectionHeader, type JimboSectionTone } from "../ui/jimboSectionHeader.js";
import { JimboText } from "../ui/jimboText.js";

export interface CardListProps {
  /** Label rendered as a JimboSectionHeader chip above the row. */
  title: string;
  /** Optional secondary line (e.g. "Ante 1"). */
  subtitle?: string;
  /** Color tone of the section header. */
  tone?: JimboSectionTone;
  /** The card-group children (typically CardFan instances). */
  children: React.ReactNode;
  className?: string;
}

/**
 * Labeled horizontal strip of card groups. Use INSIDE a JimboPanel when the
 * list stands alone as its own panel surface; pass it nested children
 * directly when it's already inside a panel. Composes JimboSectionHeader
 * for the chip-style title + a horizontal flex row of card-group children.
 *
 *     <JimboPanel>
 *       <CardList title="Shop picks" subtitle="Ante 1" tone="blue">
 *         <CardFan cards={['A_S','K_S','Q_S']} />
 *         <CardFan cards={['2_C','3_C','4_C']} />
 *       </CardList>
 *     </JimboPanel>
 */
export function CardList({ title, subtitle, tone = 'blue', children, className = "" }: CardListProps) {
  return (
    <div className={`j-card-list ${className}`.trim()}>
      <JimboSectionHeader label={title} tone={tone} />
      {subtitle ? (
        <JimboText size="xs" tone="white" style={{ textAlign: 'center', marginBottom: 4 }}>
          {subtitle}
        </JimboText>
      ) : null}
      <div className="j-card-list__row">{children}</div>
    </div>
  );
}
