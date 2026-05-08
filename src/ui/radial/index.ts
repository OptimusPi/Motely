// Jimbo UI — Radial Navigation Module
// Orbital/radial menu system for the Jammy mascot.

// Layout
export { RadialMenu } from "./RadialMenu.js";
export type { RadialMenuProps } from "./RadialMenu.js";

// Primitives
export { RadialPill } from "./RadialPill.js";
export type { RadialPillProps } from "./RadialPill.js";

export { RadialButton } from "./RadialButton.js";
export type {
    RadialButtonProps,
    RadialButtonColor,
    RadialButtonActionProps,
    RadialButtonToggleProps,
    RadialButtonCountProps,
    RadialButtonBackProps,
} from "./RadialButton.js";

export { RadialBadge } from "./RadialBadge.js";
export type { RadialBadgeProps, RadialBadgeState } from "./RadialBadge.js";

export { RadialBreadcrumb } from "./RadialBreadcrumb.js";
export type { RadialBreadcrumbProps } from "./RadialBreadcrumb.js";

// State hook
export { useRadialMenu } from "./useRadialMenu.js";
export type { UseRadialMenuProps, RadialMenuState } from "./useRadialMenu.js";

// ── Backwards-compatibility aliases ───────────────────────────────────────────
// These match the old export names from the flat RadialNavigation.tsx file.
// Consumers using the old names will keep working without import changes.
export { RadialButton as JimboRadialNavigationButton } from "./RadialButton.js";
export { RadialBadge as JimboRadialNavigationBadge } from "./RadialBadge.js";
export { RadialBreadcrumb as BreadcrumNavPill } from "./RadialBreadcrumb.js";
export { RadialPill as JimboOrbitalPill } from "./RadialPill.js";
