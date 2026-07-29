"use client";

import type { TextareaHTMLAttributes } from "react";

export type JimboTextAreaProps = TextareaHTMLAttributes<HTMLTextAreaElement>;

export function JimboTextArea({ className, style, ...rest }: JimboTextAreaProps) {
  const classes = ["j-textarea", className].filter(Boolean).join(" ");
  return <textarea className={classes} style={style} {...rest} />;
}
