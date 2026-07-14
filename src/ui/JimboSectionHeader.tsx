"use client";

import type { HTMLAttributes, ReactNode } from "react";

export type JimboSectionTone = "red" | "orange" | "blue" | "green" | "purple" | "gold" | "grey";

export interface JimboSectionHeaderProps extends HTMLAttributes<HTMLDivElement> {
  label: ReactNode;
  tone?: JimboSectionTone;
}

/** Colored tag + rule — section divider inside a panel. */
export function JimboSectionHeader({
  label,
  tone = "blue",
  className,
  ...rest
}: JimboSectionHeaderProps) {
  const classes = ["j-section-header", className].filter(Boolean).join(" ");
  return (
    <div className={classes} {...rest}>
      <span className={`j-section-header__tag j-section-header__tag--${tone}`}>{label}</span>
      <span className={`j-section-header__rule j-section-header__rule--${tone}`} aria-hidden />
    </div>
  );
}
