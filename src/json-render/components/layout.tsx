import React from "react";

/**
 * Layout primitives — Panel, Stack, Grid, Text, Spacer, Divider, Badge.
 *
 * All use CSS custom properties from jimbo-tokens.css.
 * No external classes. No BEM. Just inline styles + tokens.
 */

/* ─── Panel ─── */
export interface PanelProps {
  title?: string;
  subtitle?: string;
  variant?: "default" | "accent" | "muted";
  className?: string;
  children?: React.ReactNode;
}

export const Panel: React.FC<PanelProps> = ({
  title,
  subtitle,
  variant = "default",
  className = "",
  children,
}) => {
  const bg =
    variant === "accent"
      ? "var(--j-dark-blue)"
      : variant === "muted"
        ? "var(--j-surface-inset)"
        : "var(--j-surface)";

  return (
    <div
      className={className}
      style={{
        border: "2px solid var(--j-panel-edge)",
        borderRadius: "var(--j-radius)",
        background: bg,
        padding: "var(--j-space-4)",
        boxShadow: "var(--j-shadow)",
      }}
    >
      {title && (
        <h3
          style={{
            color: "var(--j-blue)",
            fontSize: "var(--j-text-lg)",
            margin: "0 0 var(--j-space-2) 0",
            fontFamily: "var(--j-font)",
          }}
        >
          {title}
        </h3>
      )}
      {subtitle && (
        <p
          style={{
            color: "var(--j-grey)",
            fontSize: "var(--j-text-sm)",
            margin: "0 0 var(--j-space-3) 0",
            opacity: 0.8,
          }}
        >
          {subtitle}
        </p>
      )}
      {children}
    </div>
  );
};

/* ─── Stack ─── */
export interface StackProps {
  gap?: number;
  align?: "start" | "center" | "end" | "stretch";
  className?: string;
  children?: React.ReactNode;
}

export const Stack: React.FC<StackProps> = ({
  gap = 12,
  align = "stretch",
  className = "",
  children,
}) => {
  return (
    <div
      className={className}
      style={{
        display: "flex",
        flexDirection: "column",
        gap,
        alignItems: align,
        width: "100%",
      }}
    >
      {children}
    </div>
  );
};

/* ─── Grid ─── */
export interface GridProps {
  columns?: number;
  gap?: number;
  className?: string;
  children?: React.ReactNode;
}

export const Grid: React.FC<GridProps> = ({
  columns = 3,
  gap = 16,
  className = "",
  children,
}) => {
  return (
    <div
      className={className}
      style={{
        display: "grid",
        gridTemplateColumns: `repeat(${columns}, 1fr)`,
        gap,
        width: "100%",
      }}
    >
      {children}
    </div>
  );
};

/* ─── Text ─── */
export interface TextProps {
  body: string;
  variant?: "title" | "body" | "muted" | "accent" | "error";
  className?: string;
  children?: React.ReactNode; // allows wrapping text in spans
}

export const Text: React.FC<TextProps> = ({
  body,
  variant = "body",
  className = "",
  children,
}) => {
  const colors: Record<string, string> = {
    title: "var(--j-gold)",
    body: "var(--j-grey)",
    muted: "var(--j-dark-grey)",
    accent: "var(--j-blue)",
    error: "var(--j-red)",
  };

  const sizes: Record<string, string> = {
    title: "var(--j-text-xl)",
    body: "var(--j-text-md)",
    muted: "var(--j-text-sm)",
    accent: "var(--j-text-md)",
    error: "var(--j-text-sm)",
  };

  return (
    <span
      className={className}
      style={{
        color: colors[variant] || colors.body,
        fontSize: sizes[variant] || sizes.body,
        fontFamily: "var(--j-font)",
        lineHeight: 1.4,
      }}
    >
      {body}
      {children}
    </span>
  );
};

/* ─── Spacer ─── */
export interface SpacerProps {
  size?: number;
}

export const Spacer: React.FC<SpacerProps> = ({ size = 16 }) => {
  return <div style={{ height: size, width: "100%" }} />;
};

/* ─── Divider ─── */
export interface DividerProps {
  className?: string;
}

export const Divider: React.FC<DividerProps> = ({ className = "" }) => {
  return (
    <div
      className={className}
      style={{
        height: 2,
        background: "var(--j-panel-edge)",
        width: "100%",
        margin: "var(--j-space-3) 0",
      }}
    />
  );
};

/* ─── Badge ─── */
export interface BadgeProps {
  label: string;
  tone?: "red" | "blue" | "green" | "orange" | "gold" | "purple" | "grey";
  className?: string;
}

export const Badge: React.FC<BadgeProps> = ({
  label,
  tone = "grey",
  className = "",
}) => {
  const toneMap: Record<string, { bg: string; color: string }> = {
    red: { bg: "var(--j-dark-red)", color: "var(--j-red)" },
    blue: { bg: "var(--j-dark-blue)", color: "var(--j-blue)" },
    green: { bg: "var(--j-dark-green)", color: "var(--j-green)" },
    orange: { bg: "var(--j-dark-orange)", color: "var(--j-orange)" },
    gold: { bg: "var(--j-dark-orange)", color: "var(--j-gold)" },
    purple: { bg: "var(--j-dark-purple)", color: "var(--j-purple)" },
    grey: { bg: "var(--j-surface-inset)", color: "var(--j-grey)" },
  };

  const t = toneMap[tone] || toneMap.grey;

  return (
    <span
      className={className}
      style={{
        display: "inline-flex",
        alignItems: "center",
        padding: "2px 10px",
        borderRadius: 999,
        background: t.bg,
        color: t.color,
        fontSize: "var(--j-text-xs)",
        fontFamily: "var(--j-font)",
        fontWeight: 700,
        letterSpacing: "0.5px",
        textTransform: "uppercase" as const,
      }}
    >
      {label}
    </span>
  );
};
