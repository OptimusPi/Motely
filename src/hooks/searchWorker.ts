/// <reference lib="webworker" />

// Single-threaded WASM runtime per worker. Bootsharp removed mt mode in #203,
// so no SharedArrayBuffer / COOP+COEP headers are required to deploy this.
// If a future change reintroduces SAB, switch the deployment to the Cloudflare
// permanent named tunnel so COOP/COEP can be enforced at the edge.
import { MotelySearch } from "motely-wasm";
import type { MotelyProgress, MotelyScoredSeedResult, JamlAesthetic } from "motely-wasm";
import {
    ensureMotelyReady,
    parseJaml,
    runSearch,
    setJimmolateProbe,
    enableJimmolate,
    type EngineSearchMode,
} from "../lib/motely/runtime.js";

const self = globalThis as typeof globalThis & DedicatedWorkerGlobalScope;

type StartMessage = {
    type: "start";
    mode: "aesthetic" | "seedlist" | "random";
    jaml: string;
    aesthetic?: JamlAesthetic | number;
    seeds?: string[];
    count?: number;
    predicateStr?: string;
};

let unsubscribers: Array<() => void> = [];
// motely-wasm@23 searches don't return totals; the final figures come from the
// last MotelyProgress broadcast before the search Promise resolves.
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
            seed: result.seed,
            score: result.score,
            tallyColumns: [],
        });
    };
    MotelySearch.onScoredResult.subscribe(onResult);
    unsubscribers.push(() => MotelySearch.onScoredResult.unsubscribe(onResult));

    const onProgress = (progress: MotelyProgress) => {
        lastProgress = progress;
        self.postMessage({
            type: "progress",
            searched: Number(progress.seedsSearched),
            matching: Number(progress.matchingSeeds),
            percent: progress.percentComplete,
            seedsPerMs: progress.seedsPerMillisecond,
        });
    };
    MotelySearch.onProgress.subscribe(onProgress);
    unsubscribers.push(() => MotelySearch.onProgress.unsubscribe(onProgress));

    const onSeedMatch = (seed: string) => {
        self.postMessage({ type: "match", seed });
    };
    MotelySearch.onSeedMatch.subscribe(onSeedMatch);
    unsubscribers.push(() => MotelySearch.onSeedMatch.unsubscribe(onSeedMatch));
}

function startSearchFor(message: StartMessage): Promise<void> {
    const config = parseJaml(message.jaml);
    return runSearch(config, message.mode as EngineSearchMode, {
        seeds: message.seeds,
        count: message.count,
        aesthetic: message.aesthetic,
    });
}

self.onmessage = async (event: MessageEvent) => {
    const data = event.data as StartMessage | { type: "stop" };

    if (data.type === "stop") {
        // No engine-level cancel in motely-wasm@23: the owning hook terminates this
        // worker to truly stop the search. Detach + ack so the UI can settle.
        detachListeners();
        self.postMessage({ type: "cancelled" });
        return;
    }

    if (data.type !== "start") return;

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
                status: "Completed",
                total: lastProgress ? Number(lastProgress.seedsSearched) : 0,
                matched: lastProgress ? Number(lastProgress.matchingSeeds) : 0,
            });
        } finally {
            detachListeners();
        }
    } catch (error) {
        detachListeners();
        self.postMessage({
            type: "error",
            message: error instanceof Error ? error.message : String(error),
        });
    }
};

self.postMessage({ type: "ready" });
