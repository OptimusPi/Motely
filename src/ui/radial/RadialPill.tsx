"use client";

import React from "react";
import "./radial-navigation.css";

export interface RadialPillProps {
    /** Horizontal offset from center (px). */
    x: number;
    /** Vertical offset from center (px). */
    y: number;
    /** When true, pill collapses to center (exit animation). */
    hiding: boolean;
    children: React.ReactNode;
}

/**
 * Absolutely-positioned wrapper for a radial menu item.
 *
 * Uses CSS custom properties + a shared `@starting-style` rule
 * instead of injecting per-instance `<style>` tags.
 */
export function RadialPill({ x, y, hiding, children }: RadialPillProps) {
    return (
        <div
            className="orbital-pill"
            data-hiding={hiding ? "true" : "false"}
            style={
                {
                    "--orbital-x": `${hiding ? 0 : x}px`,
                    "--orbital-y": `${hiding ? 0 : y}px`,
                } as React.CSSProperties
            }
        >
            <span className="orbital-pill-inner">{children}</span>
        </div>
    );
}
