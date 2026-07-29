"use client";

import type { InputHTMLAttributes, Ref } from "react";

export type JimboTextInputProps = InputHTMLAttributes<HTMLInputElement> & {
  ref?: Ref<HTMLInputElement>;
};

export function JimboTextInput({ className, ref, ...rest }: JimboTextInputProps) {
  const classes = ["j-text-input", className].filter(Boolean).join(" ");
  return <input ref={ref} className={classes} {...rest} />;
}
