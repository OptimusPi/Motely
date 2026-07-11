"use client";

import {
  JamlCodeEditor,
  JimmolateEditor,
  DEFAULT_JIMMOLATE_SOURCE,
  compileJimmolatePredicate,
  setJimmolatePredicate,
} from "jaml-codemirror";
import { useCallback, useMemo, useState } from "react";
import { MotelyJaml, MotelySearch } from "motely-wasm";
import { render, balatroRegistry, type JsonNode } from "../json-render/index.js";
import { JammyMascot } from "../json-render/components/mascot.js";
import { JimboButton } from "../ui/JimboButton.js";
import { JimboPanel } from "../ui/JimboPanel.js";

export interface McpSeedFinderAppProps {
  jaml: string;
  onChange: (next: string) => void;
  onRunRequest?: (jaml: string) => Promise<void> | void;
}

interface SearchResult {
  seed: string;
  score: number;
}

export function McpSeedFinderApp({ jaml, onChange, onRunRequest }: McpSeedFinderAppProps) {
  const [status, setStatus] = useState<"idle" | "running" | "completed" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<SearchResult[]>([]);
  const [totalSearched, setTotalSearched] = useState<string>("0");
  const [matchingSeeds, setMatchingSeeds] = useState<number>(0);
  const [seedsPerSecond, setSeedsPerSecond] = useState<number>(0);
  const [jimmolateSource, setJimmolateSource] = useState<string>(DEFAULT_JIMMOLATE_SOURCE);

  const runSearch = useCallback(async () => {
    if (status === "running") return;

    const validation = MotelyJaml.validate(jaml);
    if (validation) {
      setError(validation);
      setStatus("error");
      return;
    }

    try {
      setJimmolatePredicate(compileJimmolatePredicate(jimmolateSource));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
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
  }, [jaml, jimmolateSource, onRunRequest, status]);

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
      className="j-app"
      style={{ width: 320, maxWidth: 320, minWidth: 320, height: 568, maxHeight: 568, minHeight: 568, margin: "0 auto" }}
    >
      <div className="j-app__scroll">
        {render(spec, balatroRegistry)}

        <JimboPanel style={{ marginTop: 16, padding: 0, height: 120, overflow: "hidden" }}>
          <JamlCodeEditor
            value={jaml}
            onChange={onChange}
            height="100%"
            placeholder="JAML filter..."
          />
        </JimboPanel>

        <JimboPanel style={{ marginTop: 16, padding: 0, height: 100, overflow: "hidden" }}>
          <JimmolateEditor
            value={jimmolateSource}
            onChange={setJimmolateSource}
            height="100%"
            placeholder="Jimmolate predicate..."
          />
        </JimboPanel>

        {error && <span className="j-text j-text--body j-text--red j-mt-sm">{error}</span>}

        <JimboButton
          tone="red"
          className="j-btn--full j-mt-sm"
          onClick={runSearch}
          disabled={status === "running"}
        >
          {status === "running" ? "Searching..." : "Search"}
        </JimboButton>
      </div>

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
