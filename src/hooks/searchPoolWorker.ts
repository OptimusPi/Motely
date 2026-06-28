/// <reference lib="webworker" />

// Pool worker. Each instance boots its own motely-wasm runtime (single-threaded
// per Bootsharp 0.8 post-#203 — no SAB, no COOP/COEP). The owning `useSearchPool`
// hook is responsible for partitioning the input space and assigning each worker
// a disjoint slice via the fields on PoolStartMessage. This worker just runs
// what it is told.
import { MotelySearch } from "motely-wasm";
import type { MotelyProgress, MotelyScoredSeedResult, MotelyDeck, MotelyStake, JamlConfig } from "motely-wasm";
import {
    ensureMotelyReady,
    parseJaml,
    runSearch,
    setJimmolateProbe,
    enableJimmolate,
    type EngineSearchMode,
} from "../lib/motely/runtime.js";

const self = globalThis as typeof globalThis & DedicatedWorkerGlobalScope;

export type PoolSearchMode = "random" | "seedlist" | "sequential" | "aesthetic";

export interface PoolStartMessage {
    type: "start";
    workerIndex: number;
    workerCount: number;
    mode: PoolSearchMode;
    jaml: string;
    count?: number;
    seeds?: string[];
    batchCharacterCount?: number;
    startBatchIndex?: string;
    endBatchIndex?: string;
    aesthetic?: number;
    deck?: number;
    stake?: number;
    predicateStr?: string;
}

export interface PoolStopMessage {
    type: "stop";
}

export type PoolInboundMessage = PoolStartMessage | PoolStopMessage;

export interface PoolReadyMessage {
    type: "ready";
}

export interface PoolResultMessage {
    type: "result";
    workerIndex: number;
    seed: string;
    score: number;
    tallyColumns: number[];
}

export interface PoolMatchMessage {
    type: "match";
    workerIndex: number;
    seed: string;
}

export interface PoolProgressMessage {
    type: "progress";
    workerIndex: number;
    searched: number;
    matching: number;
    percent: number;
    seedsPerMs: number;
}

export interface PoolCompleteMessage {
    type: "complete";
    workerIndex: number;
    status: "Completed" | "Cancelled";
    total: number;
    matched: number;
}

export interface PoolCancelledMessage {
    type: "cancelled";
    workerIndex: number;
}

export interface PoolErrorMessage {
    type: "error";
    workerIndex: number;
    message: string;
}

export type PoolOutboundMessage =
    | PoolReadyMessage
    | PoolResultMessage
    | PoolMatchMessage
    | PoolProgressMessage
    | PoolCompleteMessage
    | PoolCancelledMessage
    | PoolErrorMessage;

let unsubscribers: Array<() => void> = [];
let workerIndex = 0;
// motely-wasm@23 searches don't return totals; capture them from the last
// MotelyProgress broadcast before the search Promise resolves.
let lastProgress: MotelyProgress | null = null;

function detachListeners(): void {
    for (const off of unsubscribers) off();
    unsubscribers = [];
}

function attachListeners(): void {
    detachListeners();
    lastProgress = null;

    const onResult = (result: MotelyScoredSeedResult) => {
        self.postMessage({
            type: "result",
            workerIndex,
            seed: result.seed,
            score: result.score,
            tallyColumns: [],
        } satisfies PoolResultMessage);
    };
    MotelySearch.onScoredResult.subscribe(onResult);
    unsubscribers.push(() => MotelySearch.onScoredResult.unsubscribe(onResult));

    const onProgress = (progress: MotelyProgress) => {
        lastProgress = progress;
        self.postMessage({
            type: "progress",
            workerIndex,
            searched: Number(progress.seedsSearched),
            matching: Number(progress.matchingSeeds),
            percent: progress.percentComplete,
            seedsPerMs: progress.seedsPerMillisecond,
        } satisfies PoolProgressMessage);
    };
    MotelySearch.onProgress.subscribe(onProgress);
    unsubscribers.push(() => MotelySearch.onProgress.unsubscribe(onProgress));

    const onSeedMatch = (seed: string) => {
        self.postMessage({
            type: "match",
            workerIndex,
            seed,
        } satisfies PoolMatchMessage);
    };
    MotelySearch.onSeedMatch.subscribe(onSeedMatch);
    unsubscribers.push(() => MotelySearch.onSeedMatch.unsubscribe(onSeedMatch));
}

// deck/stake are config fields now; the worker is single-threaded, so the old
// withThreadCount(1) is dropped (it was a no-op here).
function applyCommonOverrides(config: JamlConfig, message: PoolStartMessage): JamlConfig {
    if (typeof message.deck === "number") {
        config.deck = message.deck as MotelyDeck;
    }
    if (typeof message.stake === "number") {
        config.stake = message.stake as MotelyStake;
    }
    return config;
}

function startSearchFor(message: PoolStartMessage): Promise<void> {
    const config = applyCommonOverrides(parseJaml(message.jaml), message);
    return runSearch(config, message.mode as EngineSearchMode, {
        seeds: message.seeds,
        count: message.count,
        aesthetic: message.aesthetic,
        startBatchIndex: typeof message.startBatchIndex === "string" ? BigInt(message.startBatchIndex) : undefined,
        endBatchIndex: typeof message.endBatchIndex === "string" ? BigInt(message.endBatchIndex) : undefined,
        batchCharacterCount: typeof message.batchCharacterCount === "number" ? message.batchCharacterCount : undefined,
    });
}

self.onmessage = async (event: MessageEvent) => {
    const data = event.data as PoolInboundMessage;

    if (data.type === "stop") {
        // No engine-level cancel in motely-wasm@23: the owning hook terminates this
        // worker to truly stop. Detach + ack so the pool can settle.
        detachListeners();
        self.postMessage({ type: "cancelled", workerIndex } satisfies PoolCancelledMessage);
        return;
    }

    if (data.type !== "start") return;

    workerIndex = data.workerIndex;

    try {
        await ensureMotelyReady();

        if (data.predicateStr) {
            try {
                const pred = new Function("seed", "deck", "stake", `return (${data.predicateStr})(seed, deck, stake);`) as (seed: string, deck: number, stake: number) => boolean;
                setJimmolateProbe((seed, deck, stake) => pred(seed, deck, stake));
                enableJimmolate();
            } catch (err) {
                console.error("Failed to compile worker Jimmolate predicate:", err);
            }
        }

        attachListeners();

        try {
            await startSearchFor(data);
            self.postMessage({
                type: "complete",
                workerIndex,
                status: "Completed",
                total: lastProgress ? Number(lastProgress.seedsSearched) : 0,
                matched: lastProgress ? Number(lastProgress.matchingSeeds) : 0,
            } satisfies PoolCompleteMessage);
        } finally {
            detachListeners();
        }
    } catch (error) {
        detachListeners();
        self.postMessage({
            type: "error",
            workerIndex,
            message: error instanceof Error ? error.message : String(error),
        } satisfies PoolErrorMessage);
    }
};

self.postMessage({ type: "ready" } satisfies PoolReadyMessage);
