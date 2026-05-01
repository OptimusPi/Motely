"use client";

import React, { useLayoutEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import type { MenuItem } from "../mascot/menuConfig";
import { RadialPill } from "./RadialPill";
import { RadialButton } from "./RadialButton";
import { RadialBadge } from "./RadialBadge";

// ── Type guards ───────────────────────────────────────────────────────────────

function hasDim(item: MenuItem): item is MenuItem & { _dim?: boolean } {
    return "_dim" in item;
}

function hasSouth(item: MenuItem): boolean {
    return "_south" in item && (item as MenuItem & { _south?: boolean })._south === true;
}

function hasIcon(item: MenuItem): item is MenuItem & { icon: string } {
    return "icon" in item && typeof item.icon === "string";
}

function hasCount(item: MenuItem): item is MenuItem & { count: number } {
    return "count" in item && typeof item.count === "number";
}

function isToggleItem(item: MenuItem): item is MenuItem & { active: boolean; disabled?: boolean } {
    return "active" in item && typeof item.active === "boolean";
}

// ── Props ─────────────────────────────────────────────────────────────────────

export interface RadialMenuProps {
    /** Menu items to display. The item flagged _south is always pinned at the bottom. */
    items: MenuItem[];
    /** When true, pills animate out to center. */
    showClosing: boolean;
    /** Jammy hit target / image size (px) — used so pills clear the seed graphic. */
    mascotSizePx: number;
    /** Horizontal radius for the orbit (px). */
    orbitRadiusX: number;
    /** Vertical radius (typically same or slightly larger than horizontal). */
    orbitRadiusY: number;
    /** Vertical translation for keyboard dodge (px). */
    mascotTranslateY: number;
    /** Current menu name — used for stable React keys. */
    currentMenu: string;
    /** Callback when a menu item is clicked. */
    onItemClick: (item: MenuItem) => void;
    /** Callback for the Back button. */
    onBack: () => void;
    /** Optional breadcrumb rendered above the Back button position. */
    breadcrumb?: ReactNode;
    /** Show dedicated page controls near the south back/start button. */
    showPageControls?: boolean;
    /** Go to previous orbital menu page. */
    onPagePrev?: () => void;
    /** Go to next orbital menu page. */
    onPageNext?: () => void;
}

// ── Orbit geometry constants ──────────────────────────────────────────────────

/** Degrees reserved on each side of south (90°) for the south-pinned button. */
const FULL_CIRCLE_RAD = 2 * Math.PI;
const SOUTH_ANGLE_RAD = Math.PI / 2;

/** ~half height of a radial pill (px) — used with width for clearance vs round mascot. */
const PILL_HALF_H = 15;

/**
 * Estimated pill width for clamp math — tuned for m6x11 ~12px + padding + toggle dot.
 */
function estimatePillWidth(item: MenuItem): number {
    const label = item.label || "";
    const charW = 7.25;
    const basePad = 36;
    const hasToggle = "active" in item && typeof item.active === "boolean";
    const dot = hasToggle ? 16 : 0;
    const w = basePad + dot + label.length * charW;
    return Math.min(200, Math.max(56, w));
}

/**
 * Minimum distance from orbit center to pill center so the pill clears the round mascot bitmap.
 */
function minDistanceFromMascotCenter(mascotHalfPx: number, pillHalfW: number): number {
    const gap = 10;
    const cornerReach = Math.hypot(pillHalfW, PILL_HALF_H);
    return mascotHalfPx + gap + cornerReach;
}

/**
 * Even ellipse; if a slot sits inside the mascot keep-out, scale **outward** along the same ray
 * (`k ≥ 1`). We do **not** clamp X toward 0 — that was pulling pills **inward** onto Jammy.
 * Edge overflow is handled by orbit radius + narrow column in `SeedMascot` / layout, not here.
 */
function layoutOrbitalEllipse(
    items: MenuItem[],
    Rx: number,
    Ry: number,
    startAngle: number,
    stepRad: number,
    mascotSizePx: number,
): Array<{ item: MenuItem; x: number; y: number }> {
    if (items.length === 0) return [];

    const mascotHalf = Math.max(0, mascotSizePx) / 2;

    return items.map((item, i) => {
        const angle = startAngle + i * stepRad;
        const ux = Math.cos(angle) * Rx;
        const uy = Math.sin(angle) * Ry;
        const d0 = Math.hypot(ux, uy);
        const halfW = estimatePillWidth(item) / 2;
        const minDist = minDistanceFromMascotCenter(mascotHalf, halfW);

        if (d0 < 1e-4) {
            return { item, x: minDist, y: 0 };
        }

        const k = Math.max(1, minDist / d0);
        const x = ux * k;
        const y = uy * k;

        return { item, x, y };
    });
}

/**
 * Orbital radial menu layout engine.
 *
 * South (`_south`) stays pinned under the mascot. Everyone else sits on an ellipse
 * at equal angle steps, with a single radial push if a pill would overlap the
 * center — no iterative “solver” (that was causing mushy, uneven layouts).
 */
export function RadialMenu({
    items,
    showClosing,
    mascotSizePx,
    orbitRadiusX,
    orbitRadiusY,
    mascotTranslateY,
    currentMenu,
    onItemClick,
    onBack,
    breadcrumb,
    showPageControls = false,
    onPagePrev,
    onPageNext,
}: RadialMenuProps) {
    const rootRef = useRef<HTMLDivElement>(null);
    const [containerW, setContainerW] = useState(375);

    useLayoutEffect(() => {
        const el = rootRef.current;
        if (!el) return;
        const apply = () => setContainerW(Math.max(1, el.clientWidth));
        apply();
        const ro = new ResizeObserver(apply);
        ro.observe(el);
        return () => ro.disconnect();
    }, []);

    if (items.length === 0) return null;

    // ── Separate south-pinned item from orbital items ─────────────────────────
    const southItem = items.find(hasSouth) ?? null;
    const orbitalItems = items.filter((item) => !hasSouth(item));
    const totalSlots = orbitalItems.length + (southItem ? 1 : 0);
    const slotStepRad = FULL_CIRCLE_RAD / totalSlots;
    const orbitalStartAngle = southItem ? SOUTH_ANGLE_RAD + slotStepRad : SOUTH_ANGLE_RAD;

    // South button (Start / Back) — always clearly wider than any orbital pill.
    // Minimum 160px so it reads as the primary action even with few orbital items.
    const MIN_SOUTH_WIDTH = 148;
    const southWidth = Math.max(
        MIN_SOUTH_WIDTH,
        totalSlots <= 1
            ? Math.min(Math.round(2 * orbitRadiusX * 1.05), containerW - 16)
            : Math.min(Math.round(2 * orbitRadiusX * Math.sin(slotStepRad / 2) * 1.28), containerW - 16),
    );

    // Breadcrumb above south (Back): fixed ctr–ctr gap so it stays attached above the southmost pill.
    const southButtonY = orbitRadiusY + 8;
    const breadcrumbCenterY = southButtonY - 46;
    const pageControlWidth = Math.max(56, Math.floor(southWidth * 0.5));
    const pageControlY = southButtonY - 20;
    const pageControlLeftX = -(southWidth / 2) + pageControlWidth / 2;
    const pageControlRightX = (southWidth / 2) - pageControlWidth / 2;

    const makeClickHandler = (item: MenuItem) => (e: React.MouseEvent) => {
        e.stopPropagation();
        if (item.action === "back-action") {
            onBack();
        } else {
            onItemClick(item);
        }
    };

    return (
        <div
            ref={rootRef}
            className="pointer-events-none absolute inset-0 z-30 flex min-w-0 items-center justify-center"
            style={{ transform: `translateY(${mascotTranslateY}px)` }}
        >
            <div className="relative h-0 w-0">
                {/* Breadcrumb pill */}
                {breadcrumb ? (
                    <div
                        className="pointer-events-auto absolute"
                        style={{
                            left: 0,
                            top: breadcrumbCenterY,
                            transform: "translate(-50%, -50%)",
                            zIndex: 20,
                        }}
                    >
                        {breadcrumb}
                    </div>
                ) : null}

                {/* Orbital items — ellipse + outward-only clearance (no X-clamp toward Jammy) */}
                {(() => {
                    const solved = layoutOrbitalEllipse(
                        orbitalItems,
                        orbitRadiusX,
                        orbitRadiusY,
                        orbitalStartAngle,
                        slotStepRad,
                        mascotSizePx,
                    );
                    return solved.map(({ item, x, y }, i) => {
                        const isDim = hasDim(item) && item._dim === true;
                        const extraClass = isDim ? "opacity-40" : "";

                        return (
                            <RadialPill
                                key={`${item.label}-${i}-${currentMenu}`}
                                x={x}
                                y={y}
                                hiding={showClosing}
                            >
                                {renderItem(item, extraClass, makeClickHandler(item))}
                            </RadialPill>
                        );
                    });
                })()}

                {/* South-pinned item — always at bottom, width fills the arc gap */}
                {southItem && (
                    <RadialPill x={0} y={southButtonY} hiding={showClosing}>
                        {renderSouthItem(southItem, southWidth, makeClickHandler(southItem))}
                    </RadialPill>
                )}

                {/* Pagination controls: anchored to top-left/right of south button */}
                {southItem && showPageControls && (
                    <>
                        <RadialPill x={pageControlLeftX} y={pageControlY} hiding={showClosing}>
                            <RadialButton
                                label="<"
                                color="blue"
                                tooltip="Previous page"
                                onClick={(e) => {
                                    e.stopPropagation();
                                    onPagePrev?.();
                                }}
                                style={{ width: pageControlWidth }}
                            />
                        </RadialPill>
                        <RadialPill x={pageControlRightX} y={pageControlY} hiding={showClosing}>
                            <RadialButton
                                label=">"
                                color="blue"
                                tooltip="Next page"
                                onClick={(e) => {
                                    e.stopPropagation();
                                    onPageNext?.();
                                }}
                                style={{ width: pageControlWidth }}
                            />
                        </RadialPill>
                    </>
                )}
            </div>
        </div>
    );
}

// ── South item renderer ───────────────────────────────────────────────────────

function renderSouthItem(item: MenuItem, width: number, onClick: (e: React.MouseEvent) => void): React.ReactNode {
    const variant = item.label === "Start" ? "start" : "back";
    return (
        <RadialButton
            variant={variant}
            label={item.label}
            color="orange"
            tooltip={item.tooltip}
            onClick={onClick}
            style={{ width }}
        />
    );
}

// ── Regular item renderer ─────────────────────────────────────────────────────

function renderItem(item: MenuItem, className: string, onClick: (e: React.MouseEvent) => void): React.ReactNode {
    // Badge (display-only)
    if (item.badge) {
        return (
            <RadialBadge
                label={item.badge.label}
                state={item.badge.state}
                color={item.color}
                tooltip={item.tooltip}
            />
        );
    }

    // Toggle
    if (isToggleItem(item)) {
        return (
            <RadialButton
                variant="toggle"
                label={item.label}
                active={item.active}
                disabled={item.disabled === true}
                color={item.color}
                tooltip={item.tooltip}
                className={className}
                onClick={onClick}
            />
        );
    }

    // Count indicator (with icon or numeric count)
    if (hasIcon(item)) {
        return (
            <RadialButton
                variant="count"
                label={item.label}
                count={0}
                icon={item.icon}
                color={item.color}
                tooltip={item.tooltip}
                className={className}
                onClick={onClick}
            />
        );
    }

    if (hasCount(item)) {
        return (
            <RadialButton
                variant="count"
                label={item.label}
                count={item.count}
                color={item.color}
                tooltip={item.tooltip}
                className={className}
                onClick={onClick}
            />
        );
    }

    // Default action button
    return (
        <RadialButton
            label={item.label}
            color={item.color}
            tooltip={item.tooltip}
            className={className}
            onClick={onClick}
        />
    );
}
