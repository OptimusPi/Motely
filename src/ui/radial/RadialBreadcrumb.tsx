"use client";

import React from "react";
import { twMerge } from "tailwind-merge";

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
            className={twMerge(
                "pointer-events-auto flex items-center gap-1 rounded-[11px] px-[10px] py-[2px] font-serif text-[11.5px] font-normal shadow-[0_4px_0_0_rgba(30,30,30,0.9)] select-none sm:px-[14px]",
                className,
            )}
            style={BREADCRUMB_STYLE}
        >
            <span className="text-[9.5px] opacity-50">›</span>
            <span className="whitespace-nowrap">{label}</span>
        </div>
    );
}
