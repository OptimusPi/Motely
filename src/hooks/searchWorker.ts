/// <reference lib="webworker" />

// Single-threaded WASM runtime per worker. Bootsharp removed mt mode in #203,
// so no SharedArrayBuffer / COOP+COEP headers are required to deploy this.
// If a future change reintroduces SAB, switch the deployment to the Cloudflare
// permanent named tunnel so COOP/COEP can be enforced at the edge.
import bootsharp, { Motely } from "motely-wasm";
import type { IMotelySearch, IMotelySearchSettingsInterop, MotelyProgress, MotelyScoredSeedResult } from "motely-wasm/motely";
import type { JamlAesthetic } from "motely-wasm/motely/filters";

const self = globalThis as typeof globalThis & DedicatedWorkerGlobalScope;

type StartMessage = {
    type: "start";
    mode: "aesthetic" | "seedlist" | "random";
    jaml: string;
    aesthetic?: JamlAesthetic | number;
    seeds?: string[];
    count?: number;
};

let currentSearch: IMotelySearch | null = null;
let unsubscribers: Array<() => void> = [];

function detachListeners(): void {
    for (const off of unsubscribers) off();
    unsubscribers = [];
}

function attachListeners(): void {
    detachListeners();

    const onResult = (result: MotelyScoredSeedResult) => {
        self.postMessage({
            type: "result",
            seed: result.seed,
            score: result.score,
            tallyColumns: Array.from(result.tallies),
        });
    };
    Motely.onScoredResult.subscribe(onResult);
    unsubscribers.push(() => Motely.onScoredResult.unsubscribe(onResult));

    const onProgress = (progress: MotelyProgress) => {
        self.postMessage({
            type: "progress",
            searched: Number(progress.seedsSearched),
            matching: Number(progress.matchingSeeds),
            percent: progress.percentComplete,
            seedsPerMs: progress.seedsPerMillisecond,
        });
    };
    Motely.onProgress.subscribe(onProgress);
    unsubscribers.push(() => Motely.onProgress.unsubscribe(onProgress));

    const onSeedMatch = (seed: string) => {
        self.postMessage({ type: "match", seed });
    };
    Motely.onSeedMatch.subscribe(onSeedMatch);
    unsubscribers.push(() => Motely.onSeedMatch.unsubscribe(onSeedMatch));
}

function configureSettings(message: StartMessage): IMotelySearchSettingsInterop {
    const settings = Motely.createSearch(message.jaml).withThreadCount(1);
    if (message.mode === "aesthetic") {
        return settings.withAestheticSearch((message.aesthetic ?? 0) as JamlAesthetic);
    }
    if (message.mode === "seedlist" && message.seeds && message.seeds.length > 0) {
        return settings.withListSearch(message.seeds, message.seeds.length);
    }
    if (message.mode === "random" && typeof message.count === "number" && message.count > 0) {
        return settings.withRandomSearch(message.count);
    }
    return settings.withAestheticSearch(0 as JamlAesthetic);
}

self.onmessage = async (event: MessageEvent) => {
    const data = event.data as StartMessage | { type: "stop" };

    if (data.type === "stop") {
        currentSearch?.cancel();
        detachListeners();
        self.postMessage({ type: "cancelled" });
        return;
    }

    if (data.type !== "start") return;

    try {
        if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
            await bootsharp.boot("/bin");
        }

        attachListeners();

        currentSearch?.cancel();
        const settings = configureSettings(data);
        const search = settings.start(undefined);
        currentSearch = search;

        try {
            await search.waitForCompletionAsync(undefined);
            self.postMessage({
                type: "complete",
                status: search.isCompleted ? "Completed" : "Cancelled",
                total: Number(search.totalSeedsSearched),
                matched: Number(search.matchingSeeds),
            });
        } finally {
            detachListeners();
            currentSearch = null;
        }
    } catch (error) {
        detachListeners();
        currentSearch = null;
        self.postMessage({
            type: "error",
            message: error instanceof Error ? error.message : String(error),
        });
    }
};

self.postMessage({ type: "ready" });
