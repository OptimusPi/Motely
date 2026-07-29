"use client";

import type { HTMLAttributes } from "react";
import { FiMaximize2, FiMinimize2, FiMove } from "react-icons/fi";

export type JimboOuterTabTone = "blue" | "red" | "orange" | "green" | "purple" | "gold";

export interface JimboOuterTabProps extends HTMLAttributes<HTMLDivElement> {
  /** Pane name shown in the tab (m6x11, letter-spaced). */
  label: string;
  /** Accent color for the label; maps to `--j-*` tone tokens. */
  tone?: JimboOuterTabTone;
  /** Highlight as the live/drop-target tab. */
  active?: boolean;
  /** Current expanded state — swaps the max/min icon. */
  fullscreen?: boolean;
  /** When provided, the fullscreen toggle button renders. */
  onToggleFullscreen?: () => void;
}

/**
 * Grabbable chrome strip that sits on the OUTER top edge of a pane — the
 * handle a user drags to tear panes around a layout. Drag semantics live with
 * the consumer (this primitive just passes `draggable` + `onDrag*` through
 * `...rest`); fullscreen is a one-prop toggle. Layout is grid per design rule 1.
 */
export function JimboOuterTab({
  label,
  tone,
  active = false,
  fullscreen = false,
  onToggleFullscreen,
  className,
  children,
  ...rest
}: JimboOuterTabProps) {
  const classes = [
    "j-outer-tab",
    tone ? `j-outer-tab--${tone}` : "",
    active ? "j-outer-tab--active" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <div className={classes} {...rest}>
      <span className="j-outer-tab__grip" aria-hidden>
        <FiMove />
      </span>
      <span className="j-outer-tab__label">{label}</span>
      <span className="j-outer-tab__space">{children}</span>
      {onToggleFullscreen ? (
        <button
          type="button"
          className="j-outer-tab__fs"
          aria-label={fullscreen ? "Restore pane" : "Fullscreen pane"}
          onClick={(e) => {
            e.stopPropagation();
            onToggleFullscreen();
          }}
        >
          {fullscreen ? <FiMinimize2 /> : <FiMaximize2 />}
        </button>
      ) : null}
    </div>
  );
}
