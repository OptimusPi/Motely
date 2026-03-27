"use client";

import { useCallback, useMemo, useRef, useState } from "react";
import { MotelyWasmStatus, bootWasm, getWasmApi } from "@/components/motely-wasm-status";
import type { SeedSearchRow } from "@/lib/search-types";

const initialJaml = `name: Painted skip-fishing

deck: Painted
stake: White

must:
  - joker: TradingCard
    antes: [1]

should:
  - planet: Venus
    antes: [1, 2, 3, 4, 5]
    label: Venus
  - tarotCard: TheHierophant
    antes: [1, 2, 3, 4, 5]
    label: Hierophant
`;

export function SearchWorkspace() {
  const [jaml, setJaml] = useState(initialJaml);
  const [threads, setThreads] = useState(1);
  const [batchCharCount, setBatchCharCount] = useState(5);
  const [startBatch, setStartBatch] = useState(0);
  const [endBatch, setEndBatch] = useState(35);
  const [isSearching, setIsSearching] = useState(false);
  const [rows, setRows] = useState<SeedSearchRow[]>([]);
  const [progress, setProgress] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [elapsedMs, setElapsedMs] = useState(0);
  const searchingRef = useRef(false);

  const handleSearch = useCallback(async () => {
    setError(null);
    setRows([]);
    setProgress("Booting WASM...");

    let api: Record<string, unknown>;
    try {
      api = await bootWasm();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to boot WASM");
      setProgress("");
      return;
    }

    // Validate JAML
    if (typeof api.validateJaml === "function") {
      const validationError = api.validateJaml(jaml) as string | null;
      if (validationError) {
        setError(validationError);
        setProgress("");
        return;
      }
    }

    setIsSearching(true);
    searchingRef.current = true;

    const onResult = (seed: string, score: number) => {
      if (!searchingRef.current) return;
      setRows((prev) => [...prev, { seed, score, tallies: [] }]);
    };

    const onProgress = (searched: bigint, found: bigint, elapsed: bigint) => {
      if (!searchingRef.current) return;
      setElapsedMs(Number(elapsed));
      setProgress(
        `${Number(searched).toLocaleString()} searched | ${Number(found)} found | ${(Number(elapsed) / 1000).toFixed(1)}s`
      );
    };

    type EventLike = { subscribe: (fn: unknown) => void; unsubscribe: (fn: unknown) => void };
    const onResultEvent = api.onResult as EventLike | undefined;
    const onProgressEvent = api.onProgress as EventLike | undefined;

    onResultEvent?.subscribe?.(onResult);
    onProgressEvent?.subscribe?.(onProgress);

    try {
      const runSearch = api.runSearch as (
        jaml: string, threads: number, batchCharCount: number, startBatch: number, endBatch: number
      ) => string;

      const summary = runSearch(
        jaml,
        Math.max(1, threads),
        Math.max(1, batchCharCount),
        startBatch,
        endBatch
      );
      setProgress(summary);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Search failed");
    }

    onResultEvent?.unsubscribe?.(onResult);
    onProgressEvent?.unsubscribe?.(onProgress);
    searchingRef.current = false;
    setIsSearching(false);
  }, [jaml, threads, batchCharCount, startBatch, endBatch]);

  return (
    <main className="page-shell">
      <section className="hero">
        <h1>Motely Seed Searcher</h1>
        <p>Browser-native WASM search. No backend needed.</p>
        <MotelyWasmStatus />
      </section>

      <section className="grid">
        <div className="card panel">
          <h2>Search</h2>

          <div className="field">
            <label htmlFor="jaml">JAML</label>
            <textarea id="jaml" value={jaml} onChange={(e) => setJaml(e.target.value)} />
          </div>

          <div className="form-row">
            <div className="field">
              <label htmlFor="threads">Threads</label>
              <input id="threads" type="number" min={1} step={1} value={threads}
                onChange={(e) => setThreads(Number(e.target.value))} />
            </div>
            <div className="field">
              <label htmlFor="batchCharCount">Batch Chars</label>
              <input id="batchCharCount" type="number" min={1} max={7} step={1} value={batchCharCount}
                onChange={(e) => setBatchCharCount(Number(e.target.value))} />
            </div>
            <div className="field">
              <label htmlFor="startBatch">Start</label>
              <input id="startBatch" type="number" min={0} value={startBatch}
                onChange={(e) => setStartBatch(Number(e.target.value))} />
            </div>
            <div className="field">
              <label htmlFor="endBatch">End</label>
              <input id="endBatch" type="number" min={1} value={endBatch}
                onChange={(e) => setEndBatch(Number(e.target.value))} />
            </div>
          </div>

          <div className="actions">
            <button className="button-primary" type="button" onClick={handleSearch}
              disabled={isSearching || !getWasmApi()}>
              {isSearching ? "Searching..." : "Search (WASM)"}
            </button>
          </div>

          <div className="status-strip">
            {progress && <div className="status-pill">{progress}</div>}
            {error && <div className="status-pill error">{error}</div>}
          </div>
        </div>

        <div className="card panel">
          <h2>Results</h2>

          {rows.length > 0 ? (
            <>
              <div className="meta-grid">
                <div className="meta-card">
                  <strong>Seeds</strong>
                  <span>{rows.length}</span>
                </div>
                <div className="meta-card">
                  <strong>Elapsed</strong>
                  <span>{(elapsedMs / 1000).toFixed(1)}s</span>
                </div>
              </div>

              <div className="table-wrap">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Seed</th>
                      <th>Score</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => (
                      <tr key={row.seed}>
                        <td>{row.seed}</td>
                        <td>{row.score}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          ) : (
            <div className="empty">
              <div>
                <h3>No results yet</h3>
                <p>Run a search to see matching seeds.</p>
              </div>
            </div>
          )}
        </div>
      </section>
    </main>
  );
}
