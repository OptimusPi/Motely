import type { Motely } from 'motely-wasm';

let MotelyWasm: typeof Motely.MotelyWasm | null = null;
let MotelyWasmEvents: typeof Motely.MotelyWasmEvents | null = null;
let activeSearch: { cancel(): void } | null = null;
let activeSearchRunId = 0;

type WorkerMessage =
  | { type: "init"; url: string }
  | { type: "start"; jaml: string; mode?: string; count?: number; aesthetic?: number; seeds?: string[]; keywords?: string; padding?: string; batchCharCount?: number; startBatch?: string; endBatch?: string }
  | { type: "stop" }
  | { type: "get_tally_labels"; jaml: string };

function post(message: Record<string, unknown>) {
  self.postMessage(message);
}

function resetEventHandlers() {
  if (!MotelyWasmEvents) return;
  MotelyWasmEvents.notifyResult = () => {};
  MotelyWasmEvents.notifyProgress = () => {};
  MotelyWasmEvents.notifyComplete = () => {};
}

self.addEventListener("message", async (e: MessageEvent<WorkerMessage>) => {
  const msg = e.data;

  if (msg.type === "init") {
    try {
      const mod = await import(/* @vite-ignore */ msg.url);
      await mod.default.boot();
      const motely = mod.Motely;
      MotelyWasm = motely.MotelyWasm;
      MotelyWasmEvents = motely.MotelyWasmEvents;
      post({ type: "ready" });
    } catch (err) {
      post({ type: "error", message: String(err) });
    }
    return;
  }

  if (msg.type === "start") {
    if (!MotelyWasm) {
      post({ type: "error", message: "Not initialized" });
      return;
    }

    const validation = MotelyWasm.validateJaml(msg.jaml);
    if (validation !== "valid") {
      post({ type: "error", message: validation });
      return;
    }

    const runId = ++activeSearchRunId;

    function cleanup() {
      resetEventHandlers();
      if (runId === activeSearchRunId) {
        activeSearch = null;
      }
    }

    if (!MotelyWasmEvents) return;

    MotelyWasmEvents.notifyResult = (seed: string, score: number, tallyColumns: ArrayLike<number>) => {
      if (runId !== activeSearchRunId) return;
      post({ type: "result", seed, score, tallyColumns: Array.from(tallyColumns) });
    };

    MotelyWasmEvents.notifyProgress = (searched: bigint, matching: bigint) => {
      if (runId !== activeSearchRunId) return;
      post({ type: "progress", searched: searched.toString(), matching: matching.toString() });
    };

    MotelyWasmEvents.notifyComplete = (status: string, searched: bigint, matched: bigint) => {
      if (runId !== activeSearchRunId) return;
      cleanup();
      post({ type: "complete", status, searched: searched.toString(), matched: matched.toString() });
    };

    try {
      const mode = msg.mode || "random";

      if (mode === "random") {
        activeSearch = MotelyWasm.startRandomSearch(msg.jaml, msg.count || 1);
      } else if (mode === "aesthetic") {
        activeSearch = MotelyWasm.startAestheticSearch(msg.jaml, (msg.aesthetic as number) || 0);
      } else if (mode === "seedList") {
        activeSearch = MotelyWasm.startSeedListSearch(msg.jaml, msg.seeds || []);
      } else if (mode === "keyword") {
        activeSearch = MotelyWasm.startKeywordSearch(msg.jaml, msg.keywords || "", msg.padding || "");
      } else if (mode === "sequential") {
        activeSearch = MotelyWasm.startSequentialSearch(msg.jaml, msg.batchCharCount || 1, BigInt(msg.startBatch || "0"), BigInt(msg.endBatch || "0"));
      } else {
        post({ type: "error", message: `Unknown search mode: ${mode}` });
        cleanup();
        return;
      }
    } catch (err) {
      cleanup();
      post({ type: "error", message: String(err) });
    }
    return;
  }

  if (msg.type === "stop") {
    activeSearchRunId++;
    resetEventHandlers();
    if (activeSearch) {
      activeSearch.cancel();
      activeSearch = null;
    }
    post({ type: "cancelled" });
    return;
  }

  if (msg.type === "get_tally_labels") {
    if (!MotelyWasm) {
      post({ type: "error", message: "Not initialized" });
      return;
    }
    try {
      const labels = MotelyWasm.getTallyLabels(msg.jaml);
      post({ type: "tally_labels", labels: Array.from(labels) });
    } catch (err) {
      post({ type: "error", message: String(err) });
    }
  }
});
