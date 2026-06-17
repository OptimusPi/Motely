"use client";

import React from "react";
import "./radial-navigation.css";

export interface RadialBreadcrumbProps {
    label: string;
    title?: string;
    className?: string;
}

const BREADCRUMB_STYLE = {
    backgroundColor: "#1a1e2e",
    border: "1.5px solid rgba(255,255,255,0.18)",
    color: "#f6f0d5",
    opacity: 0.92,
} as const;

/**
 * Breadcrumb nav pill — "currently viewing" indicator.
 *
 * Sits above the Back button to show the user's position in the menu tree.
 * Non-interactive, dark styling, same pill family as orbital buttons.
 */
export function RadialBreadcrumb({ label, title, className }: RadialBreadcrumbProps) {
    return (
        <div
            role="status"
            aria-label={title ?? `In: ${label}`}
            title={title ?? `In: ${label}`}
            className={["jimbo-radial-breadcrumb", className].filter(Boolean).join(" ")}
            style={BREADCRUMB_STYLE}
        >
            <span className="jimbo-radial-breadcrumb__chevron">›</span>
            <span className="jimbo-radial-breadcrumb__label">{label}</span>
        </div>
    );
}
