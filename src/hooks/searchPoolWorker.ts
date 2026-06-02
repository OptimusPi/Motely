/// <reference lib="webworker" />

// Pool worker. Each instance boots its own motely-wasm runtime (single-threaded
// per Bootsharp 0.8 post-#203 — no SAB, no COOP/COEP). The owning `useSearchPool`
// hook is responsible for partitioning the input space and assigning each worker
// a disjoint slice via the fields on PoolStartMessage. This worker just runs
// what it is told.
import {
    Motely,
    type IMotelySearch,
    type MotelyProgress,
    type MotelyScoredSeedResult,
    type MotelyDeck,
    type MotelyStake,
    type JamlAesthetic,
    type JamlConfig
} from "motely-wasm";
import { ensureMotelyReady, setJimmolateProbe } from "../lib/motely/runtime.js";

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

let currentSearch: IMotelySearch | null = null;
let unsubscribers: Array<() => void> = [];
let workerIndex = 0;

function detachListeners(): void {
    for (const off of unsubscribers) off();
    unsubscribers = [];
}

function attachListeners(): void {
    detachListeners();

    const onResult = (result: MotelyScoredSeedResult) => {
        self.postMessage({
            type: "result",
            workerIndex,
            seed: result.seed,
            score: result.score,
            tallyColumns: Array.from(result.tallies),
        } satisfies PoolResultMessage);
    };
    Motely.onScoredResult.subscribe(onResult);
    unsubscribers.push(() => Motely.onScoredResult.unsubscribe(onResult));

    const onProgress = (progress: MotelyProgress) => {
        self.postMessage({
            type: "progress",
            workerIndex,
            searched: Number(progress.seedsSearched),
            matching: Number(progress.matchingSeeds),
            percent: progress.percentComplete,
            seedsPerMs: progress.seedsPerMillisecond,
        } satisfies PoolProgressMessage);
    };
    Motely.onProgress.subscribe(onProgress);
    unsubscribers.push(() => Motely.onProgress.unsubscribe(onProgress));

    const onSeedMatch = (seed: string) => {
        self.postMessage({
            type: "match",
            workerIndex,
            seed,
        } satisfies PoolMatchMessage);
    };
    Motely.onSeedMatch.subscribe(onSeedMatch);
    unsubscribers.push(() => Motely.onSeedMatch.unsubscribe(onSeedMatch));
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

function configureSettings(message: PoolStartMessage): IMotelySearch {
    const config = applyCommonOverrides(Motely.parseJaml(message.jaml), message);

    switch (message.mode) {
        case "aesthetic":
            return Motely.runAestheticSearch(config, (message.aesthetic ?? 0) as JamlAesthetic);
        case "seedlist": {
            config.seeds = message.seeds ?? [];
            return Motely.runSeedListSearch(config);
        }
        case "random": {
            const count = typeof message.count === "number" && message.count > 0 ? message.count : 0;
            return Motely.runRandomSearch(config, count);
        }
        case "sequential": {
            const start = typeof message.startBatchIndex === "string" ? BigInt(message.startBatchIndex) : undefined;
            const end = typeof message.endBatchIndex === "string" ? BigInt(message.endBatchIndex) : undefined;
            const batchChars = typeof message.batchCharacterCount === "number" ? message.batchCharacterCount : undefined;
            return Motely.runSequentialSearch(config, start, end, batchChars);
        }
        default:
            return Motely.runAestheticSearch(config, 0 as JamlAesthetic);
    }
}

self.onmessage = async (event: MessageEvent) => {
    const data = event.data as PoolInboundMessage;

    if (data.type === "stop") {
        currentSearch?.cancel();
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
                Motely.enableJimmolate();
            } catch (err) {
                console.error("Failed to compile worker Jimmolate predicate:", err);
            }
        }

        attachListeners();

        currentSearch?.cancel();
        const search = configureSettings(data);
        search.start();
        currentSearch = search;

        try {
            await search.waitForCompletionAsync();
            self.postMessage({
                type: "complete",
                workerIndex,
                status: search.isCompleted ? "Completed" : "Cancelled",
                total: Number(search.totalSeedsSearched),
                matched: Number(search.matchingSeeds),
            } satisfies PoolCompleteMessage);
        } finally {
            detachListeners();
            currentSearch = null;
        }
    } catch (error) {
        detachListeners();
        currentSearch = null;
        self.postMessage({
            type: "error",
            workerIndex,
            message: error instanceof Error ? error.message : String(error),
        } satisfies PoolErrorMessage);
    }
};

self.postMessage({ type: "ready" } satisfies PoolReadyMessage);
