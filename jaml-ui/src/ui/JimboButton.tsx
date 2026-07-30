"use client";

import type { ButtonHTMLAttributes } from "react";

export interface JimboButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  size?: "xs" | "sm" | "md" | "lg";
  tone?: "orange" | "red" | "blue" | "green";
  fullWidth?: boolean;
  label?: string;
}

export function JimboButton({
  size = "md",
  tone = "orange",
  fullWidth = false,
  label,
  className,
  children,
  disabled,
  ...rest
}: JimboButtonProps) {
  const classes = [
    "j-btn",
    `j-btn--${size}`,
    `j-btn--${tone}`,
    fullWidth ? "j-btn--full" : "",
    disabled ? "j-btn--disabled" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <button className={classes} disabled={disabled} {...rest}>
      <span className="j-btn__face">{label ?? children}</span>
    </button>
  );
}
