import { type FC, useState } from "react";
import { Badge, type BadgeTone } from "./layout.js";
import { JAMMY_SEED_MASCOT_DATA_URI } from "./jammySeedMascotImage.js";

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
        return (
          <button
            key={item.action + i}
            onClick={() => onAction?.(item.action)}
            style={{
              position: "absolute",
              left: "50%",
              top: "50%",
              transform: `translate(calc(-50% + ${x}px), calc(-50% + ${y}px))`,
              pointerEvents: "auto",
              background: "none",
              border: "none",
              padding: 0,
              cursor: "pointer",
            }}
          >
            <Badge label={item.label} tone={item.tone ?? "blue"} />
          </button>
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

  const handleClick = () => {
    if (menuItems && menuItems.length > 0) {
      setOpen((v) => !v);
    }
  };

  const animation =
    mood === "happy"
      ? "jammy-bounce"
      : mood === "surprised"
        ? "jammy-shake"
        : "jammy-idle";

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
      <button
        onClick={handleClick}
        style={{
          background: "none",
          border: "none",
          padding: 0,
          cursor: menuItems && menuItems.length > 0 ? "pointer" : "default",
          width: size,
          height: size,
        }}
        aria-label="Jammy mascot"
      >
        <img
          src={JAMMY_SEED_MASCOT_DATA_URI}
          alt="Jammy"
          draggable={false}
          style={{ width: "100%", height: "100%", objectFit: "contain", display: "block" }}
        />
      </button>
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
