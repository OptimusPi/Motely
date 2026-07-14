"use client";

import type { InputHTMLAttributes } from "react";

export type JimboTextInputProps = InputHTMLAttributes<HTMLInputElement>;

export function JimboTextInput({ className, ...rest }: JimboTextInputProps) {
  const classes = ["j-text-input", className].filter(Boolean).join(" ");
  return <input className={classes} {...rest} />;
}
