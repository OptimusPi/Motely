"use client";

import type { HTMLAttributes } from "react";

export type JimboInsetProps = HTMLAttributes<HTMLDivElement>;

/** Sunken well for code rows, logs, recent finds — not a second modal frame. */
export function JimboInset({ className, children, ...rest }: JimboInsetProps) {
  const classes = ["j-inset", className].filter(Boolean).join(" ");
  return (
    <div className={classes} {...rest}>
      {children}
    </div>
  );
}
