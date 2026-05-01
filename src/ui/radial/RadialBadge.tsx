"use client";

import React from "react";
import clsx from "clsx";
import { twMerge } from "tailwind-merge";
import { JimboColorOption } from "../tokens";
import "./radial-navigation.css";
import type { RadialButtonColor } from "./RadialButton";
import { BUTTON_THEMES } from "./RadialButton";

// ── Badge State ───────────────────────────────────────────────────────────────

export type RadialBadgeState = "loading" | "success" | "error" | "info" | "warning";

const INDICATOR_STYLES: Record<RadialBadgeState, { bg: string; border: string }> = {
    success: { bg: JimboColorOption.GREEN_TEXT, border: JimboColorOption.BORDER_SILVER },
    error: { bg: JimboColorOption.DARK_RED, border: JimboColorOption.DARK_GREY },
    loading: { bg: JimboColorOption.GREY, border: JimboColorOption.DARK_GREY },
    info: { bg: JimboColorOption.BLUE, border: JimboColorOption.DARK_GREY },
    warning: { bg: JimboColorOption.ORANGE, border: JimboColorOption.DARK_GREY },
};

// ── Props ─────────────────────────────────────────────────────────────────────

export interface RadialBadgeProps {
    label: string;
    state: RadialBadgeState;
    color?: RadialButtonColor;
    tooltip?: string;
    className?: string;
}

/**
 * Display-only radial status indicator.
 *
 * Visually matches RadialButton but rendered as a `<div>` with no click handler.
 * Shows a status dot (green/red/grey) + label text.
 */
export function RadialBadge({ label, state, color = "red", tooltip, className }: RadialBadgeProps) {
    const theme = BUTTON_THEMES[color];
    const altText = tooltip ?? `badge: ${label}`;

    return (
        <div
            role="status"
            aria-label={altText}
            title={altText}
            className={twMerge(
                clsx(
                    "flex min-w-[60px] items-center justify-center gap-1.5 rounded-[11px] px-[10px] py-[2px] select-none sm:min-w-[64px] sm:px-[14px]",
                    "text-[11.5px] font-normal",
                    "shadow-[var(--btn-shadow)]",
                    className,
                ),
            )}
            style={
                {
                    backgroundColor: theme.bg,
                    border: "none",
                    height: 24,
                    "--btn-shadow": `0 4px 0 0 ${theme.shadow}`,
                } as React.CSSProperties
            }
        >
            <div
                className={twMerge(
                    "h-[9px] w-[9px] shrink-0 rounded-full border",
                    (state === "error" || state === "loading") && "animate-pulse",
                )}
                style={{
                    backgroundColor: INDICATOR_STYLES[state].bg,
                    borderWidth: 1,
                    borderColor: INDICATOR_STYLES[state].border,
                    boxShadow:
                        state === "success"
                            ? "0 0 8px rgba(53,189,134,0.5)"
                            : state === "error"
                              ? "0 0 8px rgba(160,39,33,0.5)"
                              : state === "info"
                                ? "0 0 8px rgba(62,176,248,0.5)"
                                : state === "warning"
                                  ? "0 0 8px rgba(254,166,42,0.5)"
                                  : "none",
                }}
            />
            <span className="jimbo-radial-label jimbo-radial-label--badge">{label}</span>
        </div>
    );
}
