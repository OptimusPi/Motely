"use client";

import type { HTMLAttributes, ReactNode } from "react";

export interface JimboWordmarkProps extends Omit<HTMLAttributes<HTMLDivElement>, "title"> {
  title: ReactNode;
  sub?: ReactNode;
}

/** Hero title + optional subtitle — gold wordmark, grey sub. */
export function JimboWordmark({ title, sub, className, ...rest }: JimboWordmarkProps) {
  const classes = ["j-wordmark", className].filter(Boolean).join(" ");
  return (
    <div className={classes} {...rest}>
      <div className="j-wordmark__title">{title}</div>
      {sub != null && sub !== false ? <div className="j-wordmark__sub">{sub}</div> : null}
    </div>
  );
}
