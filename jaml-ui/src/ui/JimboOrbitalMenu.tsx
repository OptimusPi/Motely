"use client";

import type { HTMLAttributes, KeyboardEvent } from "react";
import { JimboBadge, type JimboBadgeTone } from "./JimboBadge.js";

// Enter/Space activate a role="button" element — the a11y contract a real
// <button> gives for free. These wrap bare content (a badge on an orbit)
// where a JimboButton's bevel/face chrome would be wrong.
function onActivateKey(handler: () => void) {
  return (e: KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      handler();
    }
  };
}

export interface JimboOrbitalMenuItem {
  label: string;
  action: string;
  tone?: JimboBadgeTone;
}

export interface JimboOrbitalMenuProps extends HTMLAttributes<HTMLDivElement> {
  items: JimboOrbitalMenuItem[];
  onAction?: (action: string) => void;
  /** Orbit radius in px. Default 90. */
  radius?: number;
}

/** Badges arranged on a circle around a center point (mascot radial menu). */
export function JimboOrbitalMenu({
  items,
  onAction,
  radius = 90,
  className = "",
  ...rest
}: JimboOrbitalMenuProps) {
  if (items.length === 0) return null;

  const step = (2 * Math.PI) / items.length;
  const start = -Math.PI / 2; // top

  return (
    <div className={["j-orbital-menu", className].filter(Boolean).join(" ")} {...rest}>
      {items.map((item, i) => {
        const angle = start + i * step;
        const x = Math.cos(angle) * radius;
        const y = Math.sin(angle) * radius;
        const activate = () => onAction?.(item.action);
        return (
          <div
            key={item.action + i}
            role="button"
            tabIndex={0}
            className="j-orbital-menu__item"
            onClick={activate}
            onKeyDown={onActivateKey(activate)}
            style={{ "--j-orbit-x": `${x}px`, "--j-orbit-y": `${y}px` } as React.CSSProperties}
          >
            <JimboBadge tone={item.tone ?? "blue"}>{item.label}</JimboBadge>
          </div>
        );
      })}
    </div>
  );
}
