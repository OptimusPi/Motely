"use client";

import { useState, type HTMLAttributes, type ReactNode } from "react";
import { FiX } from "react-icons/fi";
import { JimboIconButton } from "./JimboIconButton.js";
import { JimboRow } from "./JimboLayout.js";

export interface JimboErrorBlockProps extends Omit<HTMLAttributes<HTMLDivElement>, "title"> {
  title?: ReactNode;
  /** Render a destructive dismiss button; hides the block when pressed. */
  onDismiss?: () => void;
}

/** Red error callout — the missing React half of `.j-error-block`. */
export function JimboErrorBlock({ title, onDismiss, className, children, ...rest }: JimboErrorBlockProps) {
  const [dismissed, setDismissed] = useState(false);
  if (dismissed) return null;
  const classes = ["j-error-block", className].filter(Boolean).join(" ");
  return (
    <div className={classes} role="alert" {...rest}>
      <JimboRow justify="between" align="center">
        <div>
          {title != null && <div className="j-error-block__title">{title}</div>}
          {children}
        </div>
        {onDismiss && (
          <JimboIconButton
            size="sm"
            tone="destructive"
            aria-label="Dismiss error"
            onClick={() => {
              setDismissed(true);
              onDismiss();
            }}
          >
            <FiX />
          </JimboIconButton>
        )}
      </JimboRow>
    </div>
  );
}
