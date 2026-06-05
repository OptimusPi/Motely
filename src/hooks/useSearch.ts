"use client";

import { useState, useCallback, useRef, useEffect } from "react";
import { Program as Motely } from "motely-wasm/motely/wasm";
import type { IMotelySearch, MotelyProgress, MotelyScoredSeedResult } from "motely-wasm/motely";
import type { JamlAesthetic } from "motely-wasm/motely/filters/jaml";
import { ensureMotelyReady, setJimmolateProbe, clearJimmolateProbe } from "../lib/motely/runtime.js";

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


// Jimmolate is enabled by the caller (setJimmolateProbe + Motely.enableJimmolate),
// so this just selects the search mode against the parsed config.
function configure(jaml: string, mode: SearchMode, opts: { aesthetic?: number; seeds?: string[]; count?: number }): IMotelySearch {
    const config = Motely.parseJaml(jaml);
    if (mode === "seedlist" && opts.seeds && opts.seeds.length > 0) {
        config.seeds = opts.seeds;
        return Motely.runSeedListSearch(config);
    }
    if (mode === "random" && typeof opts.count === "number" && opts.count > 0) {
        return Motely.runRandomSearch(config, opts.count);
    }
    return Motely.runAestheticSearch(config, (opts.aesthetic ?? 0) as JamlAesthetic);
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
        async (jaml: string, mode: SearchMode, opts: { aesthetic?: number; seeds?: string[]; count?: number; predicate?: (seed: string, deck?: number, stake?: number) => boolean } = {}) => {
            try {
                await ensureMotelyReady();

                let validation = "valid";
                try { Motely.parseJaml(jaml); } catch (e) { validation = e instanceof Error ? e.message : "Invalid JAML"; }
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

                if (opts.predicate) {
                    const pred = opts.predicate;
                    setJimmolateProbe((seed, deck, stake) => pred(seed, deck, stake));
                    Motely.enableJimmolate();
                }
                const search = configure(jaml, mode, opts).start();
                searchRef.current = search;

                try {
                    await search.waitForCompletionAsync();
                    setState((s) => ({
                        ...s,
                        status: search.isCompleted ? "completed" : "cancelled",
                        totalSearched: search.totalSeedsSearched,
                        matchingSeeds: search.matchingSeeds,
                        seedsPerSecond: 0,
                    }));
                } finally {
                    if (opts.predicate) clearJimmolateProbe();
                    cleanupRef.current?.();
                    cleanupRef.current = null;
                    searchRef.current = null;
                }
            } catch (error) {
                clearJimmolateProbe();
                teardown();
                const message = error instanceof Error ? error.message : String(error);
                setState((s) => ({ ...s, status: "error", error: message, seedsPerSecond: 0 }));
            }
        },
        [teardown],
    );

    const startAesthetic = useCallback(
        (jaml: string, aesthetic: number, predicate?: (seed: string, deck?: number, stake?: number) => boolean) =>
            startSearch(jaml, "aesthetic", { aesthetic, predicate }),
        [startSearch],
    );

    const startSeedList = useCallback(
        (jaml: string, seeds: string[], predicate?: (seed: string, deck?: number, stake?: number) => boolean) =>
            startSearch(jaml, "seedlist", { seeds, predicate }),
        [startSearch],
    );

    const startRandom = useCallback(
        (jaml: string, count: number, predicate?: (seed: string, deck?: number, stake?: number) => boolean) =>
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
