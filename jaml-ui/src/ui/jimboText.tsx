"use client";

import type { HTMLAttributes } from "react";

export type JimboTextSize = "micro" | "xs" | "sm" | "md" | "lg" | "xl" | "display";
export type JimboTextTone = "white" | "grey" | "gold" | "red" | "blue" | "green" | "orange" | "purple";

export interface JimboTextProps extends HTMLAttributes<HTMLSpanElement> {
  size?: JimboTextSize;
  tone?: JimboTextTone;
}

export function JimboText({ size = "md", tone = "white", className, children, ...rest }: JimboTextProps) {
  const classes = ["j-text", `j-text--${size}`, `j-text--${tone}`, className].filter(Boolean).join(" ");
  return (
    <span className={classes} {...rest}>
      {children}
    </span>
  );
}
