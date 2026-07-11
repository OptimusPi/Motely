"use client";

import { useCallback, useState } from "react";
import { JamlCodeEditor } from "jaml-codemirror";
import {
  MotelyJaml,
  MotelyJamlyzer,
  MotelySearch,
  type MotelyJamlyzerSeedResult,
} from "motely-wasm";
import { JimboButton } from "../ui/JimboButton.js";
import { JimboPanel } from "../ui/JimboPanel.js";
import { JamlyzerView } from "./JamlyzerView.js";

interface SearchResult {
  seed: string;
  score: number;
}

export function SeedFinderApp({ initialJaml }: { initialJaml: string }) {
  const [jaml, setJaml] = useState(initialJaml);
  const [status, setStatus] = useState<"idle" | "running" | "completed" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<SearchResult[]>([]);
  const [progress, setProgress] = useState({
    seedsSearched: "0",
    matchingSeeds: "0",
    seedsPerSecond: "0",
  });
  const [analysis, setAnalysis] = useState<{
    seed: string;
    result: MotelyJamlyzerSeedResult;
    deck: ReturnType<typeof MotelyJaml.fromYaml>["deck"];
    stake: ReturnType<typeof MotelyJaml.fromYaml>["stake"];
  } | null>(null);

  const runSearch = useCallback(
    async (mode: "list" | "random" | "sequential") => {
      setStatus("running");
      setError(null);
      setResults([]);
      setAnalysis(null);

      let config: ReturnType<typeof MotelyJaml.fromYaml>;
      try {
        config = MotelyJaml.fromYaml(jaml);
      } catch (err) {
        setStatus("error");
        setError(err instanceof Error ? err.message : String(err));
        return;
      }

      const found: SearchResult[] = [];
      const onResult = (r: { seed: string; score: number }) => found.push(r);
      const onProgress = (p: {
        seedsSearched: bigint;
        matchingSeeds: bigint;
        seedsPerMillisecond: number;
      }) =>
        setProgress({
          seedsSearched: p.seedsSearched.toLocaleString(),
          matchingSeeds: p.matchingSeeds.toLocaleString(),
          seedsPerSecond: (p.seedsPerMillisecond * 1000).toLocaleString(),
        });

      MotelySearch.onScoredResult.subscribe(onResult);
      MotelySearch.onProgress.subscribe(onProgress);

      try {
        if (mode === "list") {
          await MotelySearch.searchList(config);
        } else if (mode === "random") {
          await MotelySearch.searchRandom(config, 100_000);
        } else {
          await MotelySearch.searchSequential(config, 0n, 1n, 1);
        }
        setResults(found);
        setStatus("completed");
      } catch (err) {
        setStatus("error");
        setError(err instanceof Error ? err.message : String(err));
      } finally {
        MotelySearch.onScoredResult.unsubscribe(onResult);
        MotelySearch.onProgress.unsubscribe(onProgress);
      }
    },
    [jaml]
  );

  const analyzeSeed = useCallback(
    (seed: string) => {
      try {
        const config = MotelyJaml.fromYaml(jaml);
        config.seeds = [seed];
        const [result] = MotelyJamlyzer.analyzeSeeds(config);
        setAnalysis({ seed, result, deck: config.deck, stake: config.stake });
      } catch (err) {
        setError(err instanceof Error ? err.message : String(err));
      }
    },
    [jaml]
  );

  const loadJaml = useCallback(async () => {
    const [handle] = await (window as any).showOpenFilePicker({
      types: [{ description: "JAML files", accept: { "text/yaml": [".jaml", ".yaml", ".yml"] } }],
    });
    const file = await handle.getFile();
    const text = await file.text();
    setJaml(text);
  }, []);

  const saveJaml = useCallback(async () => {
    const handle = await (window as any).showSaveFilePicker({
      suggestedName: "filter.jaml",
      types: [{ description: "JAML file", accept: { "text/yaml": [".jaml"] } }],
    });
    const writable = await handle.createWritable();
    await writable.write(jaml);
    await writable.close();
  }, [jaml]);

  return (
    <div className="j-app" style={{ width: "100%", maxWidth: "none", minWidth: 0, height: "100%", maxHeight: "none", minHeight: "100vh" }}>
      <div className="j-app__scroll" style={{ display: "grid", gridTemplateColumns: "320px 1fr", gap: 16, alignItems: "start" }}>
        <div className="j-flex-col j-gap-md">
          <span className="j-text j-text--display">Seed Finder</span>

          <div className="j-flex j-gap-sm">
            <JimboButton size="sm" tone="blue" onClick={loadJaml}>
              Load
            </JimboButton>
            <JimboButton size="sm" tone="blue" onClick={saveJaml}>
              Save
            </JimboButton>
          </div>

          <JimboPanel style={{ minHeight: 320, padding: 0, overflow: "hidden" }}>
            <JamlCodeEditor
              value={jaml}
              onChange={setJaml}
              height="100%"
              placeholder="Write your JAML filter here..."
            />
          </JimboPanel>

          <div className="j-flex j-gap-sm j-flex-wrap">
            <JimboButton onClick={() => runSearch("list")} disabled={status === "running"}>
              Search List
            </JimboButton>
            <JimboButton onClick={() => runSearch("random")} disabled={status === "running"}>
              Search Random
            </JimboButton>
            <JimboButton onClick={() => runSearch("sequential")} disabled={status === "running"}>
              Search Sequential
            </JimboButton>
          </div>

          {error && (
            <span className="j-text j-text--body j-text--red">{error}</span>
          )}

          <JimboPanel>
            <div className="j-flex j-justify-between j-items-center j-mt-sm" style={{ marginBottom: 12 }}>
              <span className="j-text j-text--sm j-text--blue">
                {status === "running" ? "Searching..." : `${results.length} results`}
              </span>
              <span className="j-text j-text--xs j-text--grey">
                {progress.seedsSearched} searched
              </span>
            </div>
            <div className="j-flex-col j-gap-sm">
              {results.map((r) => (
                <JimboButton
                  key={r.seed}
                  tone="grey"
                  size="sm"
                  className="j-btn--full"
                  onClick={() => analyzeSeed(r.seed)}
                  disabled={status === "running"}
                >
                  <span className="j-info-card__title">{r.seed}</span>{" "}
                  <span className="j-info-card__sub">Score: {r.score}</span>
                </JimboButton>
              ))}
            </div>
          </JimboPanel>
        </div>

        <div>
          {analysis ? (
            <JamlyzerView result={analysis.result} deck={analysis.deck} stake={analysis.stake} />
          ) : (
            <JimboPanel>
              <span className="j-text j-text--body j-text--grey">
                Click a seed result to run Jamlyzer and display the full ante-by-ante breakdown.
              </span>
            </JimboPanel>
          )}
        </div>
      </div>
    </div>
  );
}

export const STARTER_JAML = `must:
  - joker: Blueprint
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
deck: Red
stake: White
`;
