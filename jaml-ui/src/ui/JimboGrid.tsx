"use client";

import type { HTMLAttributes } from "react";
import type { JimboGap } from "./JimboLayout.js";

const GAP_PX: Record<JimboGap, string> = {
  xs: "var(--j-space-xs)",
  sm: "var(--j-space-sm)",
  md: "var(--j-space-md)",
  lg: "var(--j-space-lg)",
  xl: "var(--j-space-xl)",
};

export interface JimboGridProps extends HTMLAttributes<HTMLDivElement> {
  /** Fixed column count (default 3). Ignored when `minColWidth` is set. */
  columns?: number;
  /** Auto-fit as many columns of at least this many px as the width allows. */
  minColWidth?: number;
  gap?: JimboGap;
}

/**
 * Multi-column grid container. Grid is deterministic across host iframes
 * (unlike flex), so this is the only sanctioned multi-column layout.
 */
export function JimboGrid({ columns = 3, minColWidth, gap = "md", style, ...rest }: JimboGridProps) {
  const template = minColWidth
    ? `repeat(auto-fit, minmax(${minColWidth}px, 1fr))`
    : `repeat(${columns}, minmax(0, 1fr))`;
  return (
    <div
      style={{
        ...style,
        display: "grid",
        gridTemplateColumns: template,
        gap: GAP_PX[gap],
        width: "100%",
      }}
      {...rest}
    />
  );
}
