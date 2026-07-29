"use client";

import type { ButtonHTMLAttributes } from "react";

export interface JimboListItemProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  active?: boolean;
}

export function JimboListItem({ active, className, children, ...rest }: JimboListItemProps) {
  const classes = ["j-list-item", className].filter(Boolean).join(" ");
  return (
    <button type="button" className={classes} data-active={active} {...rest}>
      {children}
    </button>
  );
}
