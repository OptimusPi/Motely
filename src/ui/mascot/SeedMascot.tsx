import React from "react";

import type { MenuItem } from "./menuConfig.js";
import { RadialMenu } from "../radial/index.js";
import "./mascot.css";

export interface SeedMascotProps {
    /** Hit target size for the mascot image (px). Default 160. */
    size?: number;
    /** Vertical translation for keyboard dodge (px). */
    mascotTranslateY?: number;
    /** Extra translateY (px) for welcome entrance sliding animation. */
    welcomeEntranceLift?: number;
    /** CSS class for mascot animations (boing, dance, talking, etc.) */
    mascotMotionClass?: string;
    /** Callback when the mascot itself is tapped. */
    onTap?: () => void;

    // -- Radial Menu Props --
    /** Whether to show the radial menu ring. */
    showRadialMenu?: boolean;
    /** Whether the radial menu is currently animating out. */
    showClosingRadialMenu?: boolean;
    /** Menu items to display. */
    radialItems?: MenuItem[];
    /** Key for the current menu stack level. */
    currentMenu?: string;
    /** Callback when a menu item is clicked. */
    onItemClick?: (item: MenuItem) => void;
    /** Callback for the Back button. */
    onBack?: () => void;
    /** Show pagination controls. */
    showPageControls?: boolean;
    /** Go to previous orbital menu page. */
    onPagePrev?: () => void;
    /** Go to next orbital menu page. */
    onPageNext?: () => void;
    /** Breadcrumb trail label. */
    breadcrumb?: React.ReactNode;
    /** Base radius for orbit (will scale up if there are many items). */
    baseOrbitRadius?: number;
}

export const SeedMascot = React.memo(function SeedMascot({
    size = 160,
    mascotTranslateY = 0,
    welcomeEntranceLift = 0,
    mascotMotionClass = "jammy-jimbo-idle",
    onTap,
    showRadialMenu = false,
    showClosingRadialMenu = false,
    radialItems = [],
    currentMenu = "main",
    onItemClick,
    onBack,
    showPageControls = false,
    onPagePrev,
    onPageNext,
    breadcrumb,
    baseOrbitRadius = 66,
}: SeedMascotProps) {
    // Orbit geometry — tight ring around Jammy; small per-item nudge only when many slots.
    const orbitalCount = radialItems.filter((it) => !("_south" in it && (it as MenuItem & { _south?: boolean })._south)).length;
    const mascotHalf = size / 2;
    const minOrbitR = mascotHalf + 42;
    const orbitRadiusX = Math.max(84, baseOrbitRadius + orbitalCount * 3, minOrbitR);
    const orbitRadiusY = orbitRadiusX;

    return (
        <div className="jimbo-mascot-root">
            {/* Orbital menu items */}
            {radialItems.length > 0 && (showRadialMenu || showClosingRadialMenu) ? (
                <RadialMenu
                    items={radialItems}
                    showClosing={showClosingRadialMenu}
                    mascotSizePx={size}
                    orbitRadiusX={orbitRadiusX}
                    orbitRadiusY={orbitRadiusY}
                    mascotTranslateY={mascotTranslateY}
                    currentMenu={currentMenu}
                    onItemClick={onItemClick || (() => {})}
                    onBack={onBack || (() => {})}
                    showPageControls={showPageControls}
                    onPagePrev={onPagePrev}
                    onPageNext={onPageNext}
                    breadcrumb={breadcrumb}
                />
            ) : null}

            {/* Jammy - tappable center */}
            <div
                className="jimbo-mascot-lift"
                style={{
                    transform: `translateY(${mascotTranslateY + welcomeEntranceLift}px)`,
                }}
            >
                <button
                    type="button"
                    onClick={onTap}
                    className="jimbo-mascot-btn"
                    style={{
                        width: size,
                        height: size,
                    }}
                    aria-label="Tap Jammy to open chat or the menu"
                >
                    <span
                        className={`jimbo-mascot-img-wrap ${mascotMotionClass}`}
                        style={{ transformOrigin: "50% 80%" }}
                    >
                        <img src="/jaml-logo.png" alt="Jammy" className="jimbo-mascot-img" />
                    </span>
                </button>
            </div>
        </div>
    );
});
