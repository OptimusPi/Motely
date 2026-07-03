import { JamlCodeEditor } from "jaml-codemirror";
import { useCallback, useEffect, useMemo, useState } from "react";
import { MotelyJaml, MotelySearch } from "motely-wasm";
import { render, balatroRegistry, JammyMascot, type JsonNode } from "jaml-ui";

export interface SeedFinderAppProps {
  jaml: string;
  onChange: (next: string) => void;
  onRunRequest?: (jaml: string) => Promise<void> | void;
}

interface SearchResult {
  seed: string;
  score: number;
}

export function SeedFinderApp({ jaml, onChange, onRunRequest }: SeedFinderAppProps) {
  const [status, setStatus] = useState<"idle" | "running" | "completed" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<SearchResult[]>([]);
  const [totalSearched, setTotalSearched] = useState<string>("0");
  const [matchingSeeds, setMatchingSeeds] = useState<number>(0);
  const [seedsPerSecond, setSeedsPerSecond] = useState<number>(0);
  const [startedAt, setStartedAt] = useState<number>(0);

  const runSearch = useCallback(async () => {
    if (status === "running") return;

    const validation = MotelyJaml.validate(jaml);
    if (validation) {
      setError(validation);
      setStatus("error");
      return;
    }

    await onRunRequest?.(jaml);

    setStatus("running");
    setError(null);
    setResults([]);
    setTotalSearched("0");
    setMatchingSeeds(0);
    setSeedsPerSecond(0);
    setStartedAt(Date.now());

    const config = MotelyJaml.fromYaml(jaml);

    const onResult = (result: { seed: string; score: number }) => {
      setResults((prev) => [...prev, result].slice(0, 100));
    };

    const onProgress = (progress: {
      seedsSearched: bigint;
      matchingSeeds: bigint;
      elapsedMilliseconds: bigint;
    }) => {
      setTotalSearched(progress.seedsSearched.toString());
      setMatchingSeeds(Number(progress.matchingSeeds));
      const elapsedSec = Number(progress.elapsedMilliseconds) / 1000;
      setSeedsPerSecond(elapsedSec > 0 ? Number(progress.seedsSearched) / elapsedSec : 0);
    };

    MotelySearch.onScoredResult.subscribe(onResult);
    MotelySearch.onProgress.subscribe(onProgress);

    try {
      await MotelySearch.searchRandom(config, 1_000_000);
      setStatus("completed");
      setSeedsPerSecond(0);
    } catch (err) {
      setStatus("error");
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      MotelySearch.onScoredResult.unsubscribe(onResult);
      MotelySearch.onProgress.unsubscribe(onProgress);
    }
  }, [jaml, onRunRequest, status]);

  const spec: JsonNode = useMemo(
    () => ({
      type: "Stack",
      props: { gap: 16 },
      children: [
        {
          type: "Panel",
          props: { title: "JAML Seed Finder" },
          children: [
            {
              type: "Text",
              props: {
                body: "Edit the JAML filter and hit Search.",
                variant: "muted",
              },
            },
          ],
        },
        {
          type: "SearchStats",
          props: {
            status,
            seedsSearched: totalSearched,
            matchesFound: matchingSeeds,
            seedsPerSecond,
          },
        },
        {
          type: "Grid",
          props: { columns: 1 },
          children: results.map((r) => ({
            type: "SeedCard",
            props: {
              seed: r.seed,
              score: r.score,
            },
          })),
        },
      ],
    }),
    [status, totalSearched, matchingSeeds, seedsPerSecond, results]
  );

  return (
    <div
      style={{
        width: 320,
        height: 568,
        margin: "0 auto",
        background: "var(--j-darkest)",
        color: "var(--j-white)",
        fontFamily: "var(--j-font)",
        padding: 16,
        boxSizing: "border-box",
        overflowY: "auto",
      }}
    >
      {render(spec, balatroRegistry)}

      <div
        style={{
          width: "100%",
          height: 120,
          marginTop: 16,
          border: "2px solid var(--j-panel-edge)",
          borderRadius: "var(--j-radius)",
          overflow: "hidden",
          boxSizing: "border-box",
        }}
      >
        <JamlCodeEditor
          value={jaml}
          onChange={onChange}
          height="100%"
          placeholder="JAML filter..."
        />
      </div>

      {error && (
        <div style={{ color: "var(--j-red)", marginTop: 8, fontSize: 12 }}>{error}</div>
      )}

      <button
        onClick={runSearch}
        disabled={status === "running"}
        style={{
          width: "100%",
          marginTop: 12,
          padding: "12px 16px",
          background: status === "running" ? "var(--j-grey)" : "var(--j-red)",
          color: "var(--j-white)",
          border: "none",
          borderRadius: "var(--j-radius)",
          fontFamily: "var(--j-font)",
          fontSize: 16,
          cursor: status === "running" ? "not-allowed" : "pointer",
        }}
      >
        {status === "running" ? "Searching..." : "Search"}
      </button>

      <div
        style={{
          position: "fixed",
          right: 16,
          bottom: 16,
          zIndex: 100,
        }}
      >
        <JammyMascot
          mood={status === "running" ? "surprised" : results.length > 0 ? "happy" : "idle"}
          size={72}
          menuItems={[
            { label: "Search", action: "search", tone: "red" },
            { label: "Clear", action: "clear", tone: "grey" },
          ]}
          onMenuAction={(action) => {
            if (action === "search") runSearch();
            if (action === "clear") {
              setResults([]);
              setStatus("idle");
              setError(null);
            }
          }}
        />
      </div>
    </div>
  );
}
