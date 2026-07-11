"use client";

import type { ButtonHTMLAttributes } from "react";

export interface JimboButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  size?: "xs" | "sm" | "md" | "lg";
  tone?: "orange" | "red" | "blue" | "green" | "grey";
}

export function JimboButton({
  size = "md",
  tone = "orange",
  className,
  children,
  ...rest
}: JimboButtonProps) {
  const classes = ["j-btn", `j-btn--${size}`, `j-btn--${tone}`, className]
    .filter(Boolean)
    .join(" ");
  return (
    <button className={classes} {...rest}>
      <span className="j-btn__face">{children}</span>
    </button>
  );
}
