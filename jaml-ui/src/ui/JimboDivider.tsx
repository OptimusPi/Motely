"use client";

import type { HTMLAttributes } from "react";

export interface JimboDividerProps extends HTMLAttributes<HTMLHRElement> {
  /** Vertical rule for use inside a JimboRow. */
  vert?: boolean;
}

/** Thin hairline rule — the missing React half of `.j-divider`. */
export function JimboDivider({ vert = false, className, ...rest }: JimboDividerProps) {
  const classes = ["j-divider", vert ? "j-divider--vert" : "", className].filter(Boolean).join(" ");
  return <hr className={classes} {...rest} />;
}
