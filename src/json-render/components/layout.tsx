import React from "react";
import { JimboPanel } from "../../ui/JimboPanel.js";
import { JimboText, type JimboTextSize, type JimboTextTone } from "../../ui/jimboText.js";
import { JimboBadge, type JimboBadgeTone } from "../../ui/JimboBadge.js";
import { JimboStack, type JimboGap } from "../../ui/JimboLayout.js";
import { JimboGrid } from "../../ui/JimboGrid.js";
import { JimboSpacer } from "../../ui/JimboSpacer.js";
import { JimboDivider } from "../../ui/JimboDivider.js";
import type { JimboSectionTone } from "../../ui/JimboSectionHeader.js";

/**
 * json-render layout primitives — Panel, Stack, Grid, Text, Spacer, Divider, Badge.
 *
 * These are the node types the json-render engine maps to. They are thin adapters
 * over the real Jimbo primitives in src/ui/ — the design system with the classes
 * eyedropped from the game shader, the press-lip, the pixel fonts. Delegating
 * keeps one grammar instead of two.
 */

/* Map a pixel gap number onto the --j-space-* scale (xs 2 · sm 4 · md 8 · lg 12 · xl 16). */
function gapToken(gap: number): JimboGap {
  if (gap <= 2) return "xs";
  if (gap <= 4) return "sm";
  if (gap <= 8) return "md";
  if (gap <= 12) return "lg";
  return "xl";
}

/* ─── Panel ─── */
export interface PanelProps {
  title?: string;
  subtitle?: string;
  variant?: "default" | "accent" | "muted";
  className?: string;
  children?: React.ReactNode;
}

const PANEL_TONE: Record<NonNullable<PanelProps["variant"]>, JimboSectionTone> = {
  default: "blue",
  accent: "blue",
  muted: "grey",
};

export const Panel: React.FC<PanelProps> = ({
  title,
  subtitle,
  variant = "default",
  className,
  children,
}) => (
  <JimboPanel title={title} tone={PANEL_TONE[variant]} body className={className}>
    {subtitle ? (
      <JimboText size="sm" tone="grey">
        {subtitle}
      </JimboText>
    ) : null}
    {children}
  </JimboPanel>
);

/* ─── Stack ─── */
export interface StackProps {
  gap?: number;
  align?: "start" | "center" | "end" | "stretch";
  className?: string;
  children?: React.ReactNode;
}

export const Stack: React.FC<StackProps> = ({ gap = 12, align, className, children }) => (
  <JimboStack gap={gapToken(gap)} align={align} className={className}>
    {children}
  </JimboStack>
);

/* ─── Grid ─── */
export interface GridProps {
  columns?: number;
  gap?: number;
  className?: string;
  children?: React.ReactNode;
}

export const Grid: React.FC<GridProps> = ({ columns = 3, gap = 16, className, children }) => (
  <JimboGrid columns={columns} gap={gapToken(gap)} className={className}>
    {children}
  </JimboGrid>
);

/* ─── Text ─── */
export interface TextProps {
  body: string;
  variant?: "title" | "body" | "muted" | "accent" | "error";
  className?: string;
  children?: React.ReactNode;
}

// json-render's variants → the real (size, tone) axes JimboText exposes.
const TEXT_MAP: Record<
  NonNullable<TextProps["variant"]>,
  { size: JimboTextSize; tone: JimboTextTone }
> = {
  title: { size: "lg", tone: "gold" },
  body: { size: "md", tone: "grey" },
  muted: { size: "sm", tone: "grey" },
  accent: { size: "md", tone: "blue" },
  error: { size: "sm", tone: "red" },
};

export const Text: React.FC<TextProps> = ({ body, variant = "body", className, children }) => {
  const { size, tone } = TEXT_MAP[variant];
  return (
    <JimboText size={size} tone={tone} className={className}>
      {body}
      {children}
    </JimboText>
  );
};

/* ─── Spacer ─── */
export interface SpacerProps {
  size?: number;
}

export const Spacer: React.FC<SpacerProps> = ({ size = 16 }) => <JimboSpacer size={size} />;

/* ─── Divider ─── */
export interface DividerProps {
  className?: string;
}

export const Divider: React.FC<DividerProps> = ({ className }) => (
  <JimboDivider className={className} />
);

/* ─── Badge ─── */
export type BadgeTone = "red" | "blue" | "green" | "orange" | "gold" | "purple" | "grey";

export interface BadgeProps {
  label: string;
  tone?: BadgeTone;
  className?: string;
}

// JimboBadge has no gold tone on purpose — gold is reserved for text, not pill
// surfaces — so gold folds to the nearest warm badge, orange.
const BADGE_TONE: Record<BadgeTone, JimboBadgeTone> = {
  red: "red",
  blue: "blue",
  green: "green",
  orange: "orange",
  gold: "orange",
  purple: "purple",
  grey: "grey",
};

export function badgeToneToJimbo(tone: BadgeTone): JimboBadgeTone {
  return BADGE_TONE[tone];
}

export const Badge: React.FC<BadgeProps> = ({ label, tone = "grey", className }) => (
  <JimboBadge tone={BADGE_TONE[tone]} className={className}>
    {label}
  </JimboBadge>
);
