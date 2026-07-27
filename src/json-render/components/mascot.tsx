import { type FC } from "react";
import {
  JimboMascot,
  type JimboMascotProps,
} from "../../ui/JimboMascot.js";
import {
  JimboOrbitalMenu,
  type JimboOrbitalMenuItem,
  type JimboOrbitalMenuProps,
} from "../../ui/JimboOrbitalMenu.js";
import type { BadgeTone } from "./layout.js";
import { badgeToneToJimbo } from "./layout.js";

/**
 * json-render mascot nodes — thin adapters over the JimboMascot /
 * JimboOrbitalMenu primitives in src/ui/. The adapter exists so json-render
 * schemas can keep using the badge-tone vocabulary ("gold" included) while
 * the primitive speaks real Jimbo tones.
 */

export interface JammyOrbitalMenuItem {
  label: string;
  action: string;
  tone?: BadgeTone;
}

export interface JammyOrbitalMenuProps extends Omit<JimboOrbitalMenuProps, "items"> {
  items: JammyOrbitalMenuItem[];
}

function toJimboItems(items: JammyOrbitalMenuItem[]): JimboOrbitalMenuItem[] {
  return items.map((item) => ({
    ...item,
    tone: item.tone ? badgeToneToJimbo(item.tone) : undefined,
  }));
}

export const JammyOrbitalMenu: FC<JammyOrbitalMenuProps> = ({ items, ...rest }) => (
  <JimboOrbitalMenu items={toJimboItems(items)} {...rest} />
);

export interface JammyMascotProps extends Omit<JimboMascotProps, "menuItems"> {
  menuItems?: JammyOrbitalMenuItem[];
}

export const JammyMascot: FC<JammyMascotProps> = ({ menuItems, ...rest }) => (
  <JimboMascot menuItems={menuItems ? toJimboItems(menuItems) : undefined} {...rest} />
);
