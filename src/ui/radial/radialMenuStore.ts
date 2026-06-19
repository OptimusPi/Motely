"use client"

import { create } from "zustand"
import { JIMBO_ANIMATIONS } from "../tokens"

let closingTimer: ReturnType<typeof setTimeout> | null = null

/** Clears pending close/navigate animation timeout (e.g. unmount). */
export function clearRadialMenuTimers(): void {
    if (closingTimer !== null) {
        clearTimeout(closingTimer)
        closingTimer = null
    }
}

/** Full reset when `SeedMascot` unmounts — Zustand survives the component; hook `useState` did not. */
export function resetRadialMenuState(): void {
    clearRadialMenuTimers()
    useRadialMenuStore.setState({
        nav: { stack: [], page: 0 },
        breadcrumbStack: [],
        isClosing: false,
        announcementPrimedForDismiss: false,
    })
}

function armTimer(fn: () => void, ms: number): void {
    clearRadialMenuTimers()
    closingTimer = setTimeout(() => {
        closingTimer = null
        fn()
    }, ms)
}

export interface RadialMenuNav {
    stack: string[]
    page: number
}

interface RadialMenuSlice {
    nav: RadialMenuNav
    breadcrumbStack: string[]
    isClosing: boolean
    announcementPrimedForDismiss: boolean
    prevMaxScore: number
    prevResultCount: number
}

export interface RadialMenuStore extends RadialMenuSlice {
    open: (isWelcome: boolean) => void
    close: () => void
    back: () => void
    navigateTo: (submenuLabel: string) => void
    nextPage: (totalPages: number) => void
    prevPage: (totalPages: number) => void
    setAnnouncementPrimedForDismiss: (v: boolean) => void
    resetAnnouncementPrimed: () => void
    updateSeedResults: (seedResults: Array<{ score: number }>) => void
}

export const useRadialMenuStore = create<RadialMenuStore>((set, get) => ({
    nav: { stack: [], page: 0 },
    breadcrumbStack: [],
    isClosing: false,
    announcementPrimedForDismiss: false,
    prevMaxScore: -1,
    prevResultCount: 0,

    resetAnnouncementPrimed: () => {
        if (!get().announcementPrimedForDismiss) return;
        set({ announcementPrimedForDismiss: false });
    },

    setAnnouncementPrimedForDismiss: (v) => {
        if (get().announcementPrimedForDismiss === v) return;
        set({ announcementPrimedForDismiss: v });
    },

    open: (isWelcome) => {
        const { nav } = get()
        const target = isWelcome ? "welcome" : "main"
        // Guard: skip if already at the target root — avoids creating a new array
        // reference that would trigger Zustand subscribers and cause infinite re-renders
        // in effects that depend on the radial menu state object.
        if (nav.stack.length === 1 && nav.stack[0] === target && nav.page === 0) return
        set({ nav: { stack: [target], page: 0 } })
    },

    close: () => {
        clearRadialMenuTimers()
        set({ isClosing: true })
        armTimer(() => {
            set({ nav: { stack: [], page: 0 }, breadcrumbStack: [], isClosing: false })
        }, JIMBO_ANIMATIONS.MENU_ORBIT_DURATION)
    },

    back: () => {
        const stackBefore = get().nav.stack
        set((s) => ({ breadcrumbStack: s.breadcrumbStack.slice(0, -1) }))
        if (stackBefore.length <= 1) {
            get().close()
            return
        }
        clearRadialMenuTimers()
        set({ isClosing: true })
        armTimer(() => {
            set((s) => ({
                nav: { stack: s.nav.stack.slice(0, -1), page: 0 },
                isClosing: false,
            }))
        }, JIMBO_ANIMATIONS.MENU_SINK_DURATION)
    },

    navigateTo: (submenuLabel) => {
        clearRadialMenuTimers()
        set((s) => ({
            breadcrumbStack: [...s.breadcrumbStack, submenuLabel],
            isClosing: true,
        }))
        armTimer(() => {
            set((s) => ({
                nav: { stack: [...s.nav.stack, submenuLabel], page: 0 },
                isClosing: false,
            }))
        }, JIMBO_ANIMATIONS.MENU_SINK_DURATION)
    },

    nextPage: (totalPages) =>
        set((s) => {
            const pages = Math.max(1, totalPages)
            const next = pages <= 1 ? 0 : (s.nav.page + 1) % pages
            return { nav: { ...s.nav, page: next } }
        }),

    prevPage: (totalPages) =>
        set((s) => {
            const pages = Math.max(1, totalPages)
            const prev = pages <= 1 ? 0 : (s.nav.page - 1 + pages) % pages
            return { nav: { ...s.nav, page: prev } }
        }),

    updateSeedResults: (seedResults) => {
        const state = get();
        let shouldClose = false;

        if (seedResults.length > 0) {
            const currentMax = Math.max(...seedResults.map((r) => r.score));
            if (currentMax > state.prevMaxScore) {
                shouldClose = state.nav.stack.length > 1;
            }
        }

        if (seedResults.length > 0 && seedResults.length > state.prevResultCount) {
            shouldClose = shouldClose || state.nav.stack.length > 0;
        }

        set({
            prevMaxScore: seedResults.length > 0 ? Math.max(...seedResults.map(r => r.score)) : -1,
            prevResultCount: seedResults.length
        });

        if (shouldClose) {
            get().close();
        }
    },
}))
