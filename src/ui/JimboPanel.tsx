"use client";

import type { HTMLAttributes } from "react";

export type JimboPanelProps = HTMLAttributes<HTMLDivElement>;

export function JimboPanel({ className, children, ...rest }: JimboPanelProps) {
  const classes = ["j-panel", className].filter(Boolean).join(" ");
  return (
    <div className={classes} {...rest}>
      {children}
    </div>
  );
}
