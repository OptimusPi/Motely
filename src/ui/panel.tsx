"use client";

import type { HTMLAttributes, ReactNode } from "react";

export type JimboTone =
  | "red" | "blue" | "green" | "orange" | "purple" | "grey" | "gold"
  | "tarot" | "planet" | "spectral";

export { JimboButton } from "./JimboButton.js";

export type JimboInnerPanelProps = HTMLAttributes<HTMLDivElement>;

export function JimboInnerPanel({ className, children, ...rest }: JimboInnerPanelProps) {
  const classes = ["j-inner-panel", className].filter(Boolean).join(" ");
  return (
    <div className={classes} {...rest}>
      {children}
    </div>
  );
}

export interface JimboModalProps {
  open: boolean;
  onClose?: () => void;
  title?: ReactNode;
  className?: string;
  children?: ReactNode;
}

export function JimboModal({ open, onClose, title, className, children }: JimboModalProps) {
  if (!open) return null;
  return (
    <div className="j-modal-overlay" onClick={onClose}>
      <div
        className={["j-modal", "j-panel", className].filter(Boolean).join(" ")}
        onClick={(e) => e.stopPropagation()}
      >
        {title && <h2 className="j-modal__title">{title}</h2>}
        <div className="j-panel__body">{children}</div>
      </div>
    </div>
  );
}
