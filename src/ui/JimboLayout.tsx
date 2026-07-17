"use client";

import type { HTMLAttributes } from "react";

// The .j-stack / .j-row classes these bind to already exist in jimbo.css and are
// grid-based by design — see the "Layout primitives" section there. This file is
// only the missing React half.

/** Spacing scale — maps to the --j-space-* tokens. */
export type JimboGap = "xs" | "sm" | "md" | "lg" | "xl";

/** Cross-axis placement. */
export type JimboAlign = "start" | "center" | "end" | "stretch";

/** Main-axis distribution. */
export type JimboJustify = "start" | "center" | "end" | "between";

export type JimboLayoutProps = HTMLAttributes<HTMLDivElement> & {
  /** Space between children. Default "md" (8px). */
  gap?: JimboGap;
  align?: JimboAlign;
  justify?: JimboJustify;
};

function layoutClasses(
  base: "j-stack" | "j-row",
  { gap = "md", align, justify }: JimboLayoutProps,
  className?: string,
  extra?: string,
) {
  return [
    base,
    `${base}--gap-${gap}`,
    align && `${base}--align-${align}`,
    justify && `${base}--justify-${justify}`,
    extra,
    className,
  ]
    .filter(Boolean)
    .join(" ");
}

/** Vertical stack — the answer for "these go under each other". */
export function JimboStack({
  gap,
  align,
  justify,
  className,
  children,
  ...rest
}: JimboLayoutProps) {
  return (
    <div className={layoutClasses("j-stack", { gap, align, justify }, className)} {...rest}>
      {children}
    </div>
  );
}

export type JimboRowProps = JimboLayoutProps & {
  /** Soft-wrap onto more rows when the row runs out of width. */
  wrap?: boolean;
};

/**
 * Horizontal row — the answer for "these go next to each other". Columns size to
 * their content (`grid-auto-columns: max-content`), so this is what every
 * hand-rolled layout div in the consumer screens should collapse into.
 * Defaults to vertically centred.
 */
export function JimboRow({
  gap,
  align = "center",
  justify,
  wrap,
  className,
  children,
  ...rest
}: JimboRowProps) {
  return (
    <div
      className={layoutClasses(
        "j-row",
        { gap, align, justify },
        className,
        wrap ? "j-row--wrap" : undefined,
      )}
      {...rest}
    >
      {children}
    </div>
  );
}
