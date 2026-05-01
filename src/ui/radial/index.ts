// Jimbo UI — Radial Navigation Module
// Orbital/radial menu system for the Jammy mascot.

// Layout
export { RadialMenu } from "./RadialMenu";
export type { RadialMenuProps } from "./RadialMenu";

// Primitives
export { RadialPill } from "./RadialPill";
export type { RadialPillProps } from "./RadialPill";

export { RadialButton } from "./RadialButton";
export type {
    RadialButtonProps,
    RadialButtonColor,
    RadialButtonActionProps,
    RadialButtonToggleProps,
    RadialButtonCountProps,
    RadialButtonBackProps,
} from "./RadialButton";

export { RadialBadge } from "./RadialBadge";
export type { RadialBadgeProps, RadialBadgeState } from "./RadialBadge";

export { RadialBreadcrumb } from "./RadialBreadcrumb";
export type { RadialBreadcrumbProps } from "./RadialBreadcrumb";

// State hook
export { useRadialMenu } from "./useRadialMenu";
export type { UseRadialMenuProps, RadialMenuState } from "./useRadialMenu";

// ── Backwards-compatibility aliases ───────────────────────────────────────────
// These match the old export names from the flat RadialNavigation.tsx file.
// Consumers using the old names will keep working without import changes.
export { RadialButton as JimboRadialNavigationButton } from "./RadialButton";
export { RadialBadge as JimboRadialNavigationBadge } from "./RadialBadge";
export { RadialBreadcrumb as BreadcrumNavPill } from "./RadialBreadcrumb";
export { RadialPill as JimboOrbitalPill } from "./RadialPill";
