"use client";

import { useState, useCallback, useRef, useEffect } from "react";
import bootsharp, { Motely } from "motely-wasm";
import type { IMotelySearch, IMotelySearchSettingsInterop, MotelyProgress, MotelyScoredSeedResult } from "motely-wasm/motely";
import type { JamlAesthetic } from "motely-wasm/motely/filters";

export interface SearchResult {
    seed: string;
    score: number;
    tallyColumns?: number[];
}

export type SearchMode = "aesthetic" | "seedlist" | "random";
export type SearchStatus = "idle" | "running" | "completed" | "cancelled" | "error";

export interface UseSearchState {
    results: SearchResult[];
    totalSearched: bigint;
    matchingSeeds: bigint;
    status: SearchStatus;
    error: string | null;
    seedsPerSecond: number;
    tallyLabels: string[];
}

const INITIAL_STATE: UseSearchState = {
    results: [],
    totalSearched: 0n,
    matchingSeeds: 0n,
    status: "idle",
    error: null,
    seedsPerSecond: 0,
    tallyLabels: [],
};

async function ensureMotelyReady(): Promise<void> {
    if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
        await bootsharp.boot("/motely-wasm/bin");
    }
}

function configure(jaml: string, mode: SearchMode, opts: { aesthetic?: number; seeds?: string[]; count?: number }, withJimmolate: boolean): IMotelySearchSettingsInterop {
    const settings = Motely.createSearch(jaml);
    let configured: IMotelySearchSettingsInterop;
    if (mode === "seedlist" && opts.seeds && opts.seeds.length > 0) {
        configured = settings.withListSearch(opts.seeds, opts.seeds.length);
    } else if (mode === "random" && typeof opts.count === "number" && opts.count > 0) {
        configured = settings.withRandomSearch(opts.count);
    } else {
        configured = settings.withAestheticSearch((opts.aesthetic ?? 0) as JamlAesthetic);
    }
    return withJimmolate ? configured.withJimmolate() : configured;
}

export function useSearch() {
    const [state, setState] = useState<UseSearchState>(INITIAL_STATE);
    const searchRef = useRef<IMotelySearch | null>(null);
    const cleanupRef = useRef<(() => void) | null>(null);

    const teardown = useCallback(() => {
        cleanupRef.current?.();
        cleanupRef.current = null;
        searchRef.current?.cancel();
        searchRef.current = null;
    }, []);

    useEffect(() => () => teardown(), [teardown]);

    const startSearch = useCallback(
        async (jaml: string, mode: SearchMode, opts: { aesthetic?: number; seeds?: string[]; count?: number; predicate?: (seed: string) => boolean } = {}) => {
            try {
                await ensureMotelyReady();

                const validation = Motely.validateJaml(jaml);
                if (validation !== "valid") {
                    setState((s) => ({ ...s, status: "error", error: validation }));
                    return;
                }

                teardown();

                setState({ ...INITIAL_STATE, status: "running" });

                const onResult = (result: MotelyScoredSeedResult) => {
                    setState((s) => ({
                        ...s,
                        results: [...s.results, {
                            seed: result.seed,
                            score: result.score,
                            tallyColumns: Array.from(result.tallies),
                        }].slice(0, 1000),
                    }));
                };
                Motely.onScoredResult.subscribe(onResult);

                const onProgress = (progress: MotelyProgress) => {
                    const elapsedSec = Number(progress.elapsedMilliseconds) / 1000;
                    const sps = elapsedSec > 0 ? Number(progress.seedsSearched) / elapsedSec : 0;
                    setState((s) => ({
                        ...s,
                        totalSearched: progress.seedsSearched,
                        matchingSeeds: progress.matchingSeeds,
                        seedsPerSecond: sps,
                    }));
                };
                Motely.onProgress.subscribe(onProgress);

                cleanupRef.current = () => {
                    Motely.onScoredResult.unsubscribe(onResult);
                    Motely.onProgress.unsubscribe(onProgress);
                };

                if (opts.predicate) Motely.evalJimmolate = opts.predicate;
                const search = configure(jaml, mode, opts, !!opts.predicate).start(undefined);
                searchRef.current = search;

                try {
                    await search.waitForCompletionAsync(undefined);
                    setState((s) => ({
                        ...s,
                        status: search.isCompleted ? "completed" : "cancelled",
                        totalSearched: search.totalSeedsSearched,
                        matchingSeeds: search.matchingSeeds,
                        seedsPerSecond: 0,
                    }));
                } finally {
                    if (opts.predicate) Motely.evalJimmolate = () => true;
                    cleanupRef.current?.();
                    cleanupRef.current = null;
                    searchRef.current = null;
                }
            } catch (error) {
                Motely.evalJimmolate = () => true;
                teardown();
                const message = error instanceof Error ? error.message : String(error);
                setState((s) => ({ ...s, status: "error", error: message, seedsPerSecond: 0 }));
            }
        },
        [teardown],
    );

    const startAesthetic = useCallback(
        (jaml: string, aesthetic: number, predicate?: (seed: string) => boolean) =>
            startSearch(jaml, "aesthetic", { aesthetic, predicate }),
        [startSearch],
    );

    const startSeedList = useCallback(
        (jaml: string, seeds: string[], predicate?: (seed: string) => boolean) =>
            startSearch(jaml, "seedlist", { seeds, predicate }),
        [startSearch],
    );

    const startRandom = useCallback(
        (jaml: string, count: number, predicate?: (seed: string) => boolean) =>
            startSearch(jaml, "random", { count, predicate }),
        [startSearch],
    );

    const cancel = useCallback(() => {
        searchRef.current?.cancel();
        setState((s) => ({ ...s, status: "cancelled", seedsPerSecond: 0 }));
    }, []);

    const reset = useCallback(() => {
        teardown();
        setState(INITIAL_STATE);
    }, [teardown]);

    const clearError = useCallback(() => {
        setState((s) => (s.error || s.status === "error" ? { ...s, error: null, status: "idle" } : s));
    }, []);

    return {
        ...state,
        startAesthetic,
        startSeedList,
        startRandom,
        cancel,
        reset,
        clearError,
    };
}
