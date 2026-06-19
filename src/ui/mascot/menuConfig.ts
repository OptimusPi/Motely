// ── Strict Color Semantics ────────────────────────────────────────────────────
// RED    = Default button, opens submenu/panel/tool/modal
// BLUE   = CHAT — sends a message to Jammy specifically
// ORANGE = TOGGLE on/off state, also used for UPLOAD
export const MenuColors = {
    SUBMENU: "red" as const, // Opens submenu/panel/tool/modal
    CHAT: "blue" as const, // Sends chat message to Jammy
    TOGGLE: "orange" as const, // On/off switch
    ACTION: "red" as const, // Immediate UI action without chat message
    START: "green" as const, // Special color for START button on welcome screen
    UPLOAD: "orange" as const, // File upload action - uses Jimbo orange
};

// Simple badge for menu items (loading/success/error indicator light)
export interface IndicatorLightBadge {
    label: string;
    state: "loading" | "success" | "error" | "info" | "warning";
    detail?: string;
}

// ── Discriminated Union Types — enforces color semantics ─────────────────────
// Each button type MUST use its correct color. TypeScript will error if you try to use wrong color.

export interface SubmenuItem {
    label: string;
    color: typeof MenuColors.SUBMENU; // RED ONLY
    badge?: IndicatorLightBadge;
    tooltip?: string;
    active?: never;
    action?: never;
    // runtime styling hint – if true the pill will render lower opacity
    _dim?: boolean;
    _south?: boolean;
}

export interface ChatItem {
    label: string;
    action: string; // Chat items always have an action
    color: typeof MenuColors.CHAT; // BLUE ONLY
    badge?: never;
    tooltip?: string;
    active?: never;
    _dim?: boolean;
    _south?: boolean;
    /** Structured payload — passed through onMenuAction so handlers don't parse action strings. */
    data?: Record<string, unknown>;
}

export interface ToggleItem {
    label: string;
    action: string; // Toggle items always have an action
    color: typeof MenuColors.TOGGLE; // ORANGE ONLY
    active?: boolean; // Toggles track active state
    tooltip?: string;
    badge?: never;
    _dim?: boolean;
    _south?: boolean; // Runtime hint: pin this item at the south (bottom) of the orbit
    disabled?: boolean; // Disable if dependent feature is off
}

export interface ActionItem {
    label: string;
    action: string;
    color: typeof MenuColors.ACTION; // RED ONLY
    badge?: never;
    tooltip?: string;
    active?: never;
    _dim?: boolean;
    _south?: boolean;
    /** Structured payload — passed through onMenuAction so handlers don't parse action strings. */
    data?: Record<string, unknown>;
}

export interface StartItem {
    label: string;
    action: string;
    color: typeof MenuColors.START; // GREEN ONLY
    badge?: never;
    tooltip?: "start";
    active?: never;
    _dim?: never;
    _south?: boolean; // Runtime hint: pin this item at the south (bottom) of the orbit
}

export interface SeedItem {
    label: string;
    action: string;
    color: "seed-top" | "seed-low" | "seed-zero";
    tooltip?: string;
    badge?: never;
    active?: never;
    _south?: boolean;
    // for search result seeds: score shown as numeric badge
    count?: number;
    // for analysis result seeds: icon shown as emoji suffix
    icon?: string;
}

export interface CountIndicatorButtonItem {
    label: string;
    action?: string;
    color: typeof MenuColors.SUBMENU | typeof MenuColors.ACTION | "seed-top" | "seed-low" | "seed-zero";
    count: number;
    tooltip?: string;
    badge?: never;
    active?: never;
    _dim?: boolean;
    _south?: boolean;
    // optional emoji/icon shown instead of the count number
    icon?: string;
}

export interface UploadItem {
    label: string;
    action: string;
    color: typeof MenuColors.UPLOAD; // YELLOW ONLY
    badge?: never;
    tooltip?: string;
    active?: never;
    _dim?: boolean;
    _south?: boolean;
}

export type MenuItem = SubmenuItem | ChatItem | ToggleItem | ActionItem | StartItem | SeedItem | CountIndicatorButtonItem | UploadItem;
