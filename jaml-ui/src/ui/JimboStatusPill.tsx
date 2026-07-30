"use client";

import type { HTMLAttributes } from "react";

export type JimboStatus = "idle" | "running" | "ok" | "error" | "paused";

export interface JimboStatusPillProps extends HTMLAttributes<HTMLSpanElement> {
  status?: JimboStatus;
  label?: string;
}

/** Short single-word state indicator — the missing React half of `.j-status-pill`. */
export function JimboStatusPill({ status = "idle", label, className, children, ...rest }: JimboStatusPillProps) {
  const classes = ["j-status-pill", `j-status-pill--${status}`, className].filter(Boolean).join(" ");
  return (
    <span className={classes} {...rest}>
      <span className="j-status-pill__dot" aria-hidden />
      {label ?? children ?? status}
    </span>
  );
}
