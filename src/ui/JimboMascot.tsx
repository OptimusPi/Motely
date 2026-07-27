"use client";

import { useState, type HTMLAttributes, type KeyboardEvent } from "react";
import { JimboOrbitalMenu, type JimboOrbitalMenuItem } from "./JimboOrbitalMenu.js";
import { JAMMY_SEED_MASCOT_DATA_URI } from "./jammySeedMascotImage.js";

function onActivateKey(handler: () => void) {
  return (e: KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      handler();
    }
  };
}

export interface JimboMascotProps extends HTMLAttributes<HTMLDivElement> {
  mood?: "idle" | "happy" | "surprised";
  /** Box size in px. Default 96. */
  size?: number;
  /** When present, tapping the mascot toggles a radial action menu. */
  menuItems?: JimboOrbitalMenuItem[];
  onMenuAction?: (action: string) => void;
}

/** Jammy, the seed mascot — bounces, shakes, and hosts a radial menu. */
export function JimboMascot({
  mood = "idle",
  size = 96,
  menuItems,
  onMenuAction,
  className = "",
  ...rest
}: JimboMascotProps) {
  const [open, setOpen] = useState(false);
  const interactive = Boolean(menuItems && menuItems.length > 0);

  const handleClick = () => {
    if (interactive) setOpen((v) => !v);
  };

  const animation =
    mood === "happy" ? "jammy-bounce" : mood === "surprised" ? "jammy-shake" : "jammy-idle";

  return (
    <div
      className={["j-mascot", animation, className].filter(Boolean).join(" ")}
      style={{ "--j-mascot-size": `${size}px` } as React.CSSProperties}
      {...rest}
    >
      <div
        role={interactive ? "button" : undefined}
        tabIndex={interactive ? 0 : undefined}
        onClick={handleClick}
        onKeyDown={interactive ? onActivateKey(handleClick) : undefined}
        aria-label="Jammy mascot"
        className={interactive ? "j-mascot__hit j-mascot__hit--interactive" : "j-mascot__hit"}
      >
        <img
          src={JAMMY_SEED_MASCOT_DATA_URI}
          alt="Jammy"
          draggable={false}
          className="j-mascot__img"
        />
      </div>
      {open && menuItems && (
        <JimboOrbitalMenu
          items={menuItems}
          onAction={(action) => {
            onMenuAction?.(action);
            setOpen(false);
          }}
          radius={size * 0.9}
        />
      )}
    </div>
  );
}
