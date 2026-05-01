"use client";

import { useCallback, useEffect, useMemo } from "react";
import { useRadialMenuStore } from "./radialMenuStore";
import { useRadialViewportGeometry } from "./radialMenuViewport";

// ── Public Interface ──────────────────────────────────────────────────────────

export interface UseRadialMenuProps {
    isWelcome: boolean;
    announcementActive: boolean;
    isTalking: boolean;
    seedResults: { seed: string; score: number }[];
    onDismissAnnouncement: () => void;
}

export interface RadialMenuState {
    menuStack: string[];
    breadcrumbStack: string[];
    currentMenu: string;
    isClosing: boolean;
    showMenu: boolean;
    showClosing: boolean;
    showBack: boolean;
    menuPage: number;
    baseRadius: number;
    keyboardHeight: number;
    mascotTranslateY: number;
    open: () => void;
    close: () => void;
    back: () => void;
    navigateTo: (submenuLabel: string) => void;
    nextPage: (totalPages: number) => void;
    prevPage: (totalPages: number) => void;
    handleTap: () => void;
}

/**
 * Radial menu: **Zustand** (`useRadialMenuStore`) for stack / pagination / animations;
 * **useSyncExternalStore** (`useRadialViewportGeometry`) for keyboard + narrow-width radius.
 */
export function useRadialMenu({
    isWelcome,
    announcementActive,
    isTalking,
    seedResults,
    onDismissAnnouncement,
}: UseRadialMenuProps): RadialMenuState {
    const menuStack = useRadialMenuStore((s) => s.nav.stack);
    const menuPage = useRadialMenuStore((s) => s.nav.page);
    const breadcrumbStack = useRadialMenuStore((s) => s.breadcrumbStack);
    const isClosing = useRadialMenuStore((s) => s.isClosing);

    const storeOpen = useRadialMenuStore((s) => s.open);
    const storeClose = useRadialMenuStore((s) => s.close);
    const storeBack = useRadialMenuStore((s) => s.back);
    const storeNavigateTo = useRadialMenuStore((s) => s.navigateTo);
    const storeNextPage = useRadialMenuStore((s) => s.nextPage);
    const storePrevPage = useRadialMenuStore((s) => s.prevPage);

    const { baseRadius, keyboardHeight } = useRadialViewportGeometry();

    useEffect(() => {
        if (!announcementActive) {
            useRadialMenuStore.getState().resetAnnouncementPrimed();
        }
    }, [announcementActive]);

    const currentMenu = useMemo(
        () => (isWelcome ? "welcome" : menuStack.at(-1) ?? "main"),
        [isWelcome, menuStack],
    );

    const showMenu = useMemo(
        () => !announcementActive && (menuStack.length > 0 || seedResults.length > 0) && !isClosing,
        [announcementActive, menuStack.length, seedResults.length, isClosing],
    );

    const showClosing = useMemo(() => isClosing && menuStack.length > 0, [isClosing, menuStack.length]);

    const showBack = useMemo(() => menuStack.length >= 1, [menuStack.length]);

    const mascotTranslateY = useMemo(() => -keyboardHeight + 2, [keyboardHeight]);

    const open = useCallback(() => {
        storeOpen(isWelcome);
    }, [isWelcome, storeOpen]);

    const close = useCallback(() => {
        storeClose();
    }, [storeClose]);

    const back = useCallback(() => {
        storeBack();
    }, [storeBack]);

    const navigateTo = useCallback(
        (submenuLabel: string) => {
            storeNavigateTo(submenuLabel);
        },
        [storeNavigateTo],
    );

    const nextPage = useCallback(
        (totalPages: number) => {
            storeNextPage(totalPages);
        },
        [storeNextPage],
    );

    const prevPage = useCallback(
        (totalPages: number) => {
            storePrevPage(totalPages);
        },
        [storePrevPage],
    );

    const handleTap = useCallback(() => {
        const api = useRadialMenuStore.getState();
        if (announcementActive) {
            if (isTalking) {
                return;
            }
            onDismissAnnouncement();
            api.resetAnnouncementPrimed();
            api.open(isWelcome);
            return;
        }

        const menuIsOpen = api.nav.stack.length > 0;
        if (!menuIsOpen && !api.isClosing) {
            api.open(isWelcome);
        } else {
            api.close();
        }
    }, [announcementActive, isTalking, isWelcome, onDismissAnnouncement]);

    return useMemo(
        () => ({
            menuStack,
            breadcrumbStack,
            currentMenu,
            isClosing,
            showMenu,
            showClosing,
            showBack,
            menuPage,
            baseRadius,
            keyboardHeight,
            mascotTranslateY,
            open,
            close,
            back,
            navigateTo,
            nextPage,
            prevPage,
            handleTap,
        }),
        [
            menuStack,
            breadcrumbStack,
            currentMenu,
            isClosing,
            showMenu,
            showClosing,
            showBack,
            menuPage,
            baseRadius,
            keyboardHeight,
            mascotTranslateY,
            open,
            close,
            back,
            navigateTo,
            nextPage,
            prevPage,
            handleTap,
        ],
    );
}
