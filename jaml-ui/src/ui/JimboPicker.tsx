"use client";

import type { HTMLAttributes, ReactNode } from "react";

export function JimboPicker({ className, children, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={["j-picker", className].filter(Boolean).join(" ")} {...rest}>
      {children}
    </div>
  );
}

export function JimboPickerSection({ className, children, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={["j-picker__section", className].filter(Boolean).join(" ")} {...rest}>
      {children}
    </div>
  );
}

export function JimboPickerSearch({ className, children, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={["j-picker__search", className].filter(Boolean).join(" ")} {...rest}>
      {children}
    </div>
  );
}

export function JimboPickerHint({ className, children, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={["j-picker__hint", className].filter(Boolean).join(" ")} {...rest}>
      {children}
    </div>
  );
}

export interface JimboPickerGridProps extends HTMLAttributes<HTMLDivElement> {
  legendary?: boolean;
  scroll?: boolean;
}

export function JimboPickerGrid({ legendary, scroll: _scroll, className, children, ...rest }: JimboPickerGridProps) {
  const classes = ["j-picker__grid", legendary ? "j-picker__grid--legendary" : "", className]
    .filter(Boolean)
    .join(" ");
  return (
    <div className={classes} {...rest}>
      {children}
    </div>
  );
}

export interface JimboPickerItemProps extends HTMLAttributes<HTMLButtonElement> {
  muted?: boolean;
  title?: string;
  onClick?: () => void;
  children?: ReactNode;
}

export function JimboPickerItem({ muted, className, children, ...rest }: JimboPickerItemProps) {
  const classes = ["j-picker__item", className].filter(Boolean).join(" ");
  return (
    <button type="button" className={classes} data-muted={muted} {...rest}>
      {children}
    </button>
  );
}

export function JimboPickerPair({ className, children, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={["j-picker__pair", className].filter(Boolean).join(" ")} {...rest}>
      {children}
    </div>
  );
}

export function JimboPickerEmpty({ className, children, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={["j-picker__empty", className].filter(Boolean).join(" ")} {...rest}>
      {children}
    </div>
  );
}
