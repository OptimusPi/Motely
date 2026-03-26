"use client";

import { useMemo, useState } from "react";
import { MotelyWasmStatus } from "@/components/motely-wasm-status";
import type { SearchRequest, SearchResponse, SeedSearchRow } from "@/lib/search-types";

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
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [response, setResponse] = useState<SearchResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const rows = response?.results ?? [];
  const shouldLabels = response?.shouldLabels ?? [];

  const maxTallies = useMemo(() => {
    return Math.max(shouldLabels.length, ...rows.map((row) => row.tallies.length), 0);
  }, [rows, shouldLabels]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setError(null);

    const payload: SearchRequest = {
      jaml,
      threads: Math.max(1, Math.trunc(threads || 1)),
      batchCharCount: Math.max(1, Math.trunc(batchCharCount || 1)),
    };

    try {
      const res = await fetch("/api/search", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      const data = (await res.json()) as SearchResponse | { error?: string };
      if (!res.ok || !("ok" in data)) {
        throw new Error((data as { error?: string }).error ?? "Search request failed.");
      }

      setResponse(data);
    } catch (err) {
      setResponse(null);
      setError(err instanceof Error ? err.message : "Search request failed.");
    } finally {
      setIsSubmitting(false);
    }
  }

  function handleDemoRows() {
    const demoRows: SeedSearchRow[] = [
      { seed: "ALEEB", score: 7, tallies: [0, 0, 1, 0, 0, 5, 0, 1] },
      { seed: "J179876", score: 6, tallies: [1, 0, 0, 1, 0, 4, 0, 0] },
    ];

    setResponse({
      ok: true,
      mode: "thin-client-placeholder",
      message: "Demo rows injected locally. Replace /api/search with the real Node/C# worker contract next.",
      shouldLabels: [
        "TradingCard",
        "Rocket",
        "Venus",
        "TheHierophant",
        "Ouija",
        "PaintedShop",
        "GhostStake",
        "Extra",
      ],
      results: demoRows,
      elapsedMs: 14,
    });
    setError(null);
  }

  return (
    <main className="page-shell">
      <section className="hero">
        <h1>Motely Seed Searcher</h1>
        <p>
          Thin Vercel/Jammy client for JAML-backed search. The frontend stays pretty and fast;
          the heavy Motely work belongs in backend workers.
        </p>
        <MotelyWasmStatus />
      </section>

      <section className="grid">
        <div className="card panel">
          <h2>Search Request</h2>
          <p className="panel-copy">
            Send JAML plus light execution hints to the backend contract. This shell is already
            shaped for row-based results instead of console output.
          </p>

          <form className="form" onSubmit={handleSubmit}>
            <div className="field">
              <label htmlFor="jaml">JAML</label>
              <textarea id="jaml" value={jaml} onChange={(e) => setJaml(e.target.value)} />
              <small>Use the canonical JAML document text directly. No file-loader path needed here.</small>
            </div>

            <div className="form-row">
              <div className="field">
                <label htmlFor="threads">Threads</label>
                <input
                  id="threads"
                  type="number"
                  min={1}
                  step={1}
                  value={threads}
                  onChange={(e) => setThreads(Number(e.target.value))}
                />
                <small>For browser LLVM today, keep this at 1 unless the backend worker says otherwise.</small>
              </div>

              <div className="field">
                <label htmlFor="batchCharCount">Batch Char Count</label>
                <input
                  id="batchCharCount"
                  type="number"
                  min={1}
                  max={7}
                  step={1}
                  value={batchCharCount}
                  onChange={(e) => setBatchCharCount(Number(e.target.value))}
                />
                <small>Execution hint for the worker search backend.</small>
              </div>
            </div>

            <div className="actions">
              <button className="button-primary" type="submit" disabled={isSubmitting}>
                {isSubmitting ? "Sending…" : "Send Search Request"}
              </button>
              <button className="button-secondary" type="button" onClick={handleDemoRows} disabled={isSubmitting}>
                Load Demo Rows
              </button>
            </div>
          </form>

          <div className="status-strip">
            <div className="status-pill">Frontend role: pretty renderer</div>
            <div className="status-pill">Backend role: Node/C# worker search</div>
            {response ? <div className="status-pill">{response.message}</div> : null}
            {error ? <div className="status-pill error">{error}</div> : null}
          </div>
        </div>

        <div className="card panel">
          <h2>Result Rows</h2>
          <p className="panel-copy">
            Render one row into cards, tables, exports, or whatever Jammy wants. The frontend is
            cheap; the search engine is the expensive part.
          </p>

          {response ? (
            <div className="meta-grid">
              <div className="meta-card">
                <strong>Rows</strong>
                <span>{response.results.length}</span>
              </div>
              <div className="meta-card">
                <strong>Elapsed</strong>
                <span>{response.elapsedMs} ms</span>
              </div>
            </div>
          ) : null}

          {rows.length > 0 ? (
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>Seed</th>
                    <th>Score</th>
                    {Array.from({ length: maxTallies }).map((_, index) => (
                      <th key={index}>{shouldLabels[index] ?? `should_${index + 1}`}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={`${row.seed}-${row.score}`}>
                      <td>{row.seed}</td>
                      <td>{row.score}</td>
                      {Array.from({ length: maxTallies }).map((_, index) => (
                        <td key={index}>{row.tallies[index] ?? 0}</td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="empty">
              <div>
                <h3>No rows yet</h3>
                <p>Submit a request or load demo rows to preview the table contract.</p>
              </div>
            </div>
          )}
        </div>
      </section>
    </main>
  );
}
