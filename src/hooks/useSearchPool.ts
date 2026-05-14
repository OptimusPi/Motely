"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Motely, ensureMotelyReady } from "../motelyBoot.js";
import type { JamlAesthetic } from "motely-wasm/motely/filters";
import type {
    PoolInboundMessage,
    PoolOutboundMessage,
    PoolStartMessage,
} from "./searchPoolWorker.js";
import type { SearchMode, SearchResult, SearchStatus } from "./useSearch.js";

const DEFAULT_RESULT_CAP = 1000;
const DEFAULT_TERMINATION_GRACE_MS = 2000;

export interface UseSearchPoolState {
    results: SearchResult[];
    totalSearched: bigint;
    matchingSeeds: bigint;
    status: SearchStatus;
    error: string | null;
    seedsPerSecond: number;
    tallyLabels: string[];
    workerCount: number;
}

export interface UseSearchPoolOptions {
    workerCount?: number;
    resultCap?: number;
    terminationGraceMs?: number;
}

export interface StartPoolOptions {
    aesthetic?: number;
    seeds?: string[];
    count?: number;
    batchCharacterCount?: number;
    deck?: number;
    stake?: number;
}

interface WorkerProgress {
    searched: number;
    matching: number;
    seedsPerMs: number;
}

function defaultWorkerCount(): number {
    if (typeof navigator !== "undefined" && typeof navigator.hardwareConcurrency === "number") {
        return Math.max(1, Math.min(navigator.hardwareConcurrency, 8));
    }
    return 4;
}

function makeInitialState(workerCount: number): UseSearchPoolState {
    return {
        results: [],
        totalSearched: 0n,
        matchingSeeds: 0n,
        status: "idle",
        error: null,
        seedsPerSecond: 0,
        tallyLabels: [],
        workerCount,
    };
}

function partitionRandom(count: number, workerCount: number): number[] {
    const base = Math.floor(count / workerCount);
    const remainder = count - base * workerCount;
    return Array.from({ length: workerCount }, (_, i) => base + (i < remainder ? 1 : 0));
}

function partitionList<T>(items: T[], workerCount: number): T[][] {
    const result: T[][] = Array.from({ length: workerCount }, () => []);
    for (let i = 0; i < items.length; i++) {
        result[i % workerCount].push(items[i]);
    }
    return result;
}

interface SequentialPartition {
    start: bigint;
    end: bigint;
}

function partitionSequential(batchCharacterCount: number, workerCount: number): SequentialPartition[] {
    const total = 35n ** BigInt(batchCharacterCount);
    const base = total / BigInt(workerCount);
    const remainder = total - base * BigInt(workerCount);
    const result: SequentialPartition[] = [];
    let cursor = 0n;
    for (let i = 0; i < workerCount; i++) {
        const size = base + (BigInt(i) < remainder ? 1n : 0n);
        result.push({ start: cursor, end: cursor + size });
        cursor += size;
    }
    return result;
}

interface WorkerHandle {
    worker: Worker;
    index: number;
    settled: boolean;
}

type WorkerCtor = new () => Worker;
let cachedWorkerCtor: WorkerCtor | null = null;

async function loadWorkerCtor(): Promise<WorkerCtor> {
    if (cachedWorkerCtor) return cachedWorkerCtor;
    const mod = (await import("./searchPoolWorker?worker")) as { default: WorkerCtor };
    cachedWorkerCtor = mod.default;
    return cachedWorkerCtor;
}

export function useSearchPool(options: UseSearchPoolOptions = {}) {
    const requestedWorkerCount = options.workerCount ?? defaultWorkerCount();
    const resultCap = options.resultCap ?? DEFAULT_RESULT_CAP;
    const terminationGraceMs = options.terminationGraceMs ?? DEFAULT_TERMINATION_GRACE_MS;

    const [state, setState] = useState<UseSearchPoolState>(() => makeInitialState(requestedWorkerCount));

    const workersRef = useRef<WorkerHandle[]>([]);
    const progressRef = useRef<Map<number, WorkerProgress>>(new Map());
    const completionsRef = useRef(0);
    const expectedRef = useRef(0);
    const startedAtRef = useRef(0);
    const seenSeedsRef = useRef<Set<string>>(new Set());
    const cancelledRef = useRef(false);

    const terminateAll = useCallback(() => {
        for (const handle of workersRef.current) {
            try {
                handle.worker.terminate();
            } catch {
                // ignore
            }
        }
        workersRef.current = [];
    }, []);

    const reset = useCallback(() => {
        cancelledRef.current = false;
        terminateAll();
        progressRef.current = new Map();
        seenSeedsRef.current = new Set();
        completionsRef.current = 0;
        expectedRef.current = 0;
        setState(makeInitialState(requestedWorkerCount));
    }, [requestedWorkerCount, terminateAll]);

    useEffect(() => () => {
        terminateAll();
    }, [terminateAll]);

    const recomputeAggregate = useCallback(() => {
        let searched = 0;
        let matching = 0;
        let seedsPerMs = 0;
        for (const value of progressRef.current.values()) {
            searched += value.searched;
            matching += value.matching;
            seedsPerMs += value.seedsPerMs;
        }
        const seedsPerSecond = seedsPerMs * 1000;
        setState((s) => ({
            ...s,
            totalSearched: BigInt(searched),
            matchingSeeds: BigInt(matching),
            seedsPerSecond,
        }));
    }, []);

    const onWorkerMessage = useCallback(
        (event: MessageEvent<PoolOutboundMessage>) => {
            const data = event.data;
            switch (data.type) {
                case "ready":
                    return;
                case "result": {
                    if (seenSeedsRef.current.has(data.seed)) return;
                    seenSeedsRef.current.add(data.seed);
                    const next: SearchResult = {
                        seed: data.seed,
                        score: data.score,
                        tallyColumns: data.tallyColumns,
                    };
                    setState((s) => ({
                        ...s,
                        results: s.results.length >= resultCap ? s.results : [...s.results, next],
                    }));
                    return;
                }
                case "match":
                    return;
                case "progress": {
                    progressRef.current.set(data.workerIndex, {
                        searched: data.searched,
                        matching: data.matching,
                        seedsPerMs: data.seedsPerMs,
                    });
                    recomputeAggregate();
                    return;
                }
                case "complete": {
                    progressRef.current.set(data.workerIndex, {
                        searched: data.total,
                        matching: data.matched,
                        seedsPerMs: 0,
                    });
                    completionsRef.current += 1;
                    recomputeAggregate();
                    if (completionsRef.current >= expectedRef.current) {
                        setState((s) => ({
                            ...s,
                            status: cancelledRef.current ? "cancelled" : "completed",
                            seedsPerSecond: 0,
                        }));
                    }
                    return;
                }
                case "cancelled": {
                    progressRef.current.set(data.workerIndex, progressRef.current.get(data.workerIndex) ?? {
                        searched: 0,
                        matching: 0,
                        seedsPerMs: 0,
                    });
                    return;
                }
                case "error": {
                    completionsRef.current += 1;
                    setState((s) => ({
                        ...s,
                        status: "error",
                        error: data.message,
                        seedsPerSecond: 0,
                    }));
                    return;
                }
            }
        },
        [recomputeAggregate, resultCap],
    );

    const cancel = useCallback(() => {
        cancelledRef.current = true;
        const handles = workersRef.current;
        for (const handle of handles) {
            try {
                handle.worker.postMessage({ type: "stop" } satisfies PoolInboundMessage);
            } catch {
                // ignore
            }
        }
        setState((s) => (s.status === "running" ? { ...s, status: "cancelled", seedsPerSecond: 0 } : s));
        window.setTimeout(() => {
            terminateAll();
        }, terminationGraceMs);
    }, [terminateAll, terminationGraceMs]);

    const spawn = useCallback(
        async (workerCount: number, builders: PoolStartMessage[]) => {
            terminateAll();
            progressRef.current = new Map();
            seenSeedsRef.current = new Set();
            completionsRef.current = 0;
            expectedRef.current = workerCount;
            cancelledRef.current = false;
            startedAtRef.current = Date.now();

            const Ctor = await loadWorkerCtor();

            const handles: WorkerHandle[] = [];
            for (let i = 0; i < workerCount; i++) {
                const worker = new Ctor();
                worker.onmessage = onWorkerMessage;
                worker.onerror = (event) => {
                    completionsRef.current += 1;
                    setState((s) => ({
                        ...s,
                        status: "error",
                        error: event.message || "worker error",
                        seedsPerSecond: 0,
                    }));
                };
                worker.postMessage(builders[i] satisfies PoolInboundMessage);
                handles.push({ worker, index: i, settled: false });
            }
            workersRef.current = handles;
        },
        [onWorkerMessage, terminateAll],
    );

    const startSearch = useCallback(
        async (jaml: string, mode: SearchMode | "sequential", opts: StartPoolOptions = {}) => {
            try {
                await ensureMotelyReady();
                const validation = Motely.validateJaml(jaml);
                if (validation !== "valid") {
                    setState((s) => ({ ...s, status: "error", error: validation }));
                    return;
                }

                if (mode === "aesthetic") {
                    // Aesthetic providers hold a shared enumerator inside a single
                    // runtime — each independent WASM runtime would restart from
                    // the beginning and produce duplicates. Pool is single-worker
                    // for this mode.
                    const builders: PoolStartMessage[] = [{
                        type: "start",
                        workerIndex: 0,
                        workerCount: 1,
                        mode: "aesthetic",
                        jaml,
                        aesthetic: opts.aesthetic ?? 0,
                        deck: opts.deck,
                        stake: opts.stake,
                    }];
                    setState({ ...makeInitialState(1), status: "running", workerCount: 1 });
                    await spawn(1, builders);
                    return;
                }

                const workerCount = Math.max(1, requestedWorkerCount);
                let builders: PoolStartMessage[] = [];

                if (mode === "random") {
                    const total = Math.max(0, opts.count ?? 0);
                    const counts = partitionRandom(total, workerCount);
                    builders = counts.map((count, workerIndex) => ({
                        type: "start" as const,
                        workerIndex,
                        workerCount,
                        mode: "random" as const,
                        jaml,
                        count,
                        deck: opts.deck,
                        stake: opts.stake,
                    }));
                } else if (mode === "seedlist") {
                    const slices = partitionList(opts.seeds ?? [], workerCount);
                    builders = slices.map((seeds, workerIndex) => ({
                        type: "start" as const,
                        workerIndex,
                        workerCount,
                        mode: "seedlist" as const,
                        jaml,
                        seeds,
                        deck: opts.deck,
                        stake: opts.stake,
                    }));
                } else if (mode === "sequential") {
                    const batchCharacterCount = opts.batchCharacterCount ?? 4;
                    const ranges = partitionSequential(batchCharacterCount, workerCount);
                    builders = ranges.map((range, workerIndex) => ({
                        type: "start" as const,
                        workerIndex,
                        workerCount,
                        mode: "sequential" as const,
                        jaml,
                        batchCharacterCount,
                        startBatchIndex: range.start.toString(),
                        endBatchIndex: range.end.toString(),
                        deck: opts.deck,
                        stake: opts.stake,
                    }));
                }

                setState({ ...makeInitialState(workerCount), status: "running", workerCount });
                await spawn(workerCount, builders);
            } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                setState((s) => ({ ...s, status: "error", error: message, seedsPerSecond: 0 }));
            }
        },
        [requestedWorkerCount, spawn],
    );

    const startRandom = useCallback(
        (jaml: string, count: number, opts: StartPoolOptions = {}) =>
            startSearch(jaml, "random", { ...opts, count }),
        [startSearch],
    );

    const startSeedList = useCallback(
        (jaml: string, seeds: string[], opts: StartPoolOptions = {}) =>
            startSearch(jaml, "seedlist", { ...opts, seeds }),
        [startSearch],
    );

    const startSequential = useCallback(
        (jaml: string, batchCharacterCount: number, opts: StartPoolOptions = {}) =>
            startSearch(jaml, "sequential", { ...opts, batchCharacterCount }),
        [startSearch],
    );

    const startAesthetic = useCallback(
        (jaml: string, aesthetic: JamlAesthetic | number, opts: StartPoolOptions = {}) =>
            startSearch(jaml, "aesthetic", { ...opts, aesthetic: aesthetic as number }),
        [startSearch],
    );

    const clearError = useCallback(() => {
        setState((s) => (s.error || s.status === "error" ? { ...s, error: null, status: "idle" } : s));
    }, []);

    return {
        ...state,
        startRandom,
        startSeedList,
        startSequential,
        startAesthetic,
        cancel,
        reset,
        clearError,
    };
}
