"use client";

import type { HTMLAttributes, ReactNode } from "react";
import { JimboSectionHeader, type JimboSectionTone } from "./JimboSectionHeader.js";

export interface JimboPanelProps extends Omit<HTMLAttributes<HTMLDivElement>, "title"> {
  /** Optional section tag at the top of the panel */
  title?: ReactNode;
  tone?: JimboSectionTone;
  /** Wrap children in .j-panel__body (default true when title set, else false for flex freestyle) */
  body?: boolean;
}

export function JimboPanel({
  title,
  tone = "blue",
  body,
  className,
  children,
  ...rest
}: JimboPanelProps) {
  const classes = ["j-panel", className].filter(Boolean).join(" ");
  const wrapBody = body ?? Boolean(title);
  return (
    <div className={classes} {...rest}>
      {title != null && title !== false ? <JimboSectionHeader label={title} tone={tone} /> : null}
      {wrapBody ? <div className="j-panel__body">{children}</div> : children}
    </div>
  );
}
