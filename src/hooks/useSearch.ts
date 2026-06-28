"use client";

import { useState, useCallback, useRef, useEffect } from "react";
import { MotelySearch } from "motely-wasm";
import type { MotelyProgress, MotelyScoredSeedResult } from "motely-wasm";
import {
    ensureMotelyReady,
    parseJaml,
    runSearch,
    setJimmolateProbe,
    clearJimmolateProbe,
    enableJimmolate,
    type EngineSearchMode,
} from "../lib/motely/runtime.js";

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

export function useSearch() {
    const [state, setState] = useState<UseSearchState>(INITIAL_STATE);
    const cleanupRef = useRef<(() => void) | null>(null);
    // motely-wasm@23 has no engine-level cancel; this flag just decides whether the
    // terminal status reads "cancelled" once the in-flight Promise settles.
    const cancelledRef = useRef(false);

    const teardown = useCallback(() => {
        cleanupRef.current?.();
        cleanupRef.current = null;
    }, []);

    useEffect(() => () => teardown(), [teardown]);

    const startSearch = useCallback(
        async (jaml: string, mode: SearchMode, opts: { aesthetic?: number; seeds?: string[]; count?: number; predicate?: (seed: string, deck?: number, stake?: number) => boolean } = {}) => {
            try {
                await ensureMotelyReady();

                let config;
                try {
                    config = parseJaml(jaml);
                } catch (e) {
                    setState((s) => ({ ...s, status: "error", error: e instanceof Error ? e.message : "Invalid JAML" }));
                    return;
                }

                teardown();
                cancelledRef.current = false;
                setState({ ...INITIAL_STATE, status: "running" });

                const onResult = (result: MotelyScoredSeedResult) => {
                    setState((s) => ({
                        ...s,
                        results: [...s.results, { seed: result.seed, score: result.score }].slice(0, 1000),
                    }));
                };
                MotelySearch.onScoredResult.subscribe(onResult);

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
                MotelySearch.onProgress.subscribe(onProgress);

                cleanupRef.current = () => {
                    MotelySearch.onScoredResult.unsubscribe(onResult);
                    MotelySearch.onProgress.unsubscribe(onProgress);
                };

                if (opts.predicate) {
                    const pred = opts.predicate;
                    setJimmolateProbe((seed, deck, stake) => pred(seed, deck, stake));
                    enableJimmolate();
                }

                try {
                    await runSearch(config, mode as EngineSearchMode, {
                        seeds: opts.seeds,
                        count: opts.count,
                        aesthetic: opts.aesthetic,
                    });
                    setState((s) => ({
                        ...s,
                        status: cancelledRef.current ? "cancelled" : "completed",
                        seedsPerSecond: 0,
                    }));
                } finally {
                    if (opts.predicate) {
                        clearJimmolateProbe();
                        enableJimmolate(false);
                    }
                    teardown();
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
        // No engine-level cancel in motely-wasm@23: stop ingesting results and mark
        // the UI cancelled. The in-flight search finishes in the background.
        cancelledRef.current = true;
        teardown();
        setState((s) => ({ ...s, status: "cancelled", seedsPerSecond: 0 }));
    }, [teardown]);

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
