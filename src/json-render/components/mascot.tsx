import { type FC, type KeyboardEvent, useState } from "react";
import { Badge, type BadgeTone } from "./layout.js";
import { JAMMY_SEED_MASCOT_DATA_URI } from "./jammySeedMascotImage.js";

// Enter/Space activate a role="button" element — the a11y contract a real
// <button> gives for free. These wrap bare content (an image, a badge) where a
// JimboButton's bevel/face chrome would be wrong, so they follow SeedCard's
// role="button" div pattern rather than the raw <button> the design rules forbid.
function onActivateKey(handler: () => void) {
  return (e: KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      handler();
    }
  };
}

export interface JammyOrbitalMenuItem {
  label: string;
  action: string;
  tone?: BadgeTone;
}

export interface JammyOrbitalMenuProps {
  items: JammyOrbitalMenuItem[];
  onAction?: (action: string) => void;
  radius?: number;
  className?: string;
}

export const JammyOrbitalMenu: FC<JammyOrbitalMenuProps> = ({
  items,
  onAction,
  radius = 90,
  className = "",
}) => {
  if (items.length === 0) return null;

  const step = (2 * Math.PI) / items.length;
  const start = -Math.PI / 2; // top

  return (
    <div
      className={className}
      style={{
        position: "absolute",
        inset: 0,
        pointerEvents: "none",
      }}
    >
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
            onClick={activate}
            onKeyDown={onActivateKey(activate)}
            style={{
              position: "absolute",
              left: "50%",
              top: "50%",
              transform: `translate(calc(-50% + ${x}px), calc(-50% + ${y}px))`,
              pointerEvents: "auto",
              cursor: "pointer",
            }}
          >
            <Badge label={item.label} tone={item.tone ?? "blue"} />
          </div>
        );
      })}
    </div>
  );
};

export interface JammyMascotProps {
  mood?: "idle" | "happy" | "surprised";
  size?: number;
  menuItems?: JammyOrbitalMenuItem[];
  onMenuAction?: (action: string) => void;
  className?: string;
}

export const JammyMascot: FC<JammyMascotProps> = ({
  mood = "idle",
  size = 96,
  menuItems,
  onMenuAction,
  className = "",
}) => {
  const [open, setOpen] = useState(false);
  const interactive = Boolean(menuItems && menuItems.length > 0);

  const handleClick = () => {
    if (interactive) setOpen((v) => !v);
  };

  const animation =
    mood === "happy" ? "jammy-bounce" : mood === "surprised" ? "jammy-shake" : "jammy-idle";

  return (
    <div
      className={`${className} ${animation}`.trim()}
      style={{
        position: "relative",
        width: size,
        height: size,
        display: "inline-block",
      }}
    >
      <div
        role={interactive ? "button" : undefined}
        tabIndex={interactive ? 0 : undefined}
        onClick={handleClick}
        onKeyDown={interactive ? onActivateKey(handleClick) : undefined}
        aria-label="Jammy mascot"
        style={{
          cursor: interactive ? "pointer" : "default",
          width: size,
          height: size,
        }}
      >
        <img
          src={JAMMY_SEED_MASCOT_DATA_URI}
          alt="Jammy"
          draggable={false}
          style={{ width: "100%", height: "100%", objectFit: "contain", display: "block" }}
        />
      </div>
      {open && menuItems && (
        <JammyOrbitalMenu
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
};
