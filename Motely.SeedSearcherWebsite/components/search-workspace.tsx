"use client";

import { useCallback, useRef, useState } from "react";
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
  const [batchCharCount, setBatchCharCount] = useState(1);
  const [startBatch, setStartBatch] = useState(0);
  const [endBatch, setEndBatch] = useState(0);
  const [isSearching, setIsSearching] = useState(false);
  const [rows, setRows] = useState<SeedSearchRow[]>([]);
  const [progress, setProgress] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [elapsedMs, setElapsedMs] = useState(0);
  const searchingRef = useRef(false);

  const handleReset = useCallback(() => {
    setRows([]);
    setError(null);
    setProgress("");
    setElapsedMs(0);
  }, []);

  const handleSearch = useCallback(async () => {
    setError(null);
    setRows([]);
    setProgress("Booting WASM...");

    const api = await bootWasm().catch((err) => {
      setError(err instanceof Error ? err.message : "Failed to boot WASM");
      setProgress("");
      return null;
    });
    if (!api) return;

    try {
      const validationError = typeof api.validateJaml === "function" ? api.validateJaml(jaml) : null;
      if (validationError) {
        setError(validationError);
        setProgress("");
        return;
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
          `${Number(searched).toLocaleString()} searched · ${Number(found)} found · ${(Number(elapsed) / 1000).toFixed(1)}s`
        );
      };

      const onResultEvent = api.onResult;
      const onProgressEvent = api.onProgress;

      onResultEvent?.subscribe?.(onResult);
      onProgressEvent?.subscribe?.(onProgress);

      try {
        if (typeof api.runSearch !== "function") {
          throw new Error("runSearch() is missing from motely-wasm");
        }
        const summary = api.runSearch(
          jaml,
          Math.max(1, threads),
          Math.max(1, batchCharCount),
          startBatch,
          endBatch
        );
        setProgress(summary);
      } finally {
        onResultEvent?.unsubscribe?.(onResult);
        onProgressEvent?.unsubscribe?.(onProgress);
        searchingRef.current = false;
        setIsSearching(false);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Search failed");
      searchingRef.current = false;
      setIsSearching(false);
    }
  }, [jaml, threads, batchCharCount, startBatch, endBatch]);

  return (
    <main className="page-shell">
      <section className="hero">
        <div className="hero-badge">100% WASM</div>
        <h1>Motely WASM Probe</h1>
        <p>Mobile-first test page for pure browser Balatro seed search. No fake server path, no redirect shell, no B.S.</p>
        <MotelyWasmStatus />
      </section>

      <section className="grid">
        <div className="card panel panel-search">
          <div className="panel-heading">
            <div>
              <h2>Search</h2>
              <p className="panel-copy">Paste JAML, pick a batch window, and hit the real package.</p>
            </div>
            <div className="actions actions-inline">
              <button className="button-primary" type="button" onClick={handleSearch}
                disabled={isSearching || !getWasmApi()}>
                {isSearching ? "Searching..." : "Run Search"}
              </button>
              <button className="button-secondary" type="button" onClick={() => setJaml(initialJaml)}>
                Reset JAML
              </button>
              <button className="button-secondary" type="button" onClick={handleReset}>
                Clear
              </button>
            </div>
          </div>

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

          <div className="status-strip">
            {progress && <div className="status-pill">{progress}</div>}
            {error && <div className="status-pill error">{error}</div>}
          </div>
        </div>

        <div className="card panel">
          <div className="panel-heading">
            <div>
              <h2>Results</h2>
              <p className="panel-copy">Real-time seed matches from the browser engine.</p>
            </div>
          </div>

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
                      <th>#</th>
                      <th>Seed</th>
                      <th>Score</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row, index) => (
                      <tr key={`${row.seed}-${index}`}>
                        <td>{index + 1}</td>
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
                <p>Boot the package, run a search, and watch the rows land here.</p>
              </div>
            </div>
          )}
        </div>
      </section>
    </main>
  );
}
